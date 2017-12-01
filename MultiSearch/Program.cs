using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace MultiSearch
{
    class Program
    {
        static string ResultFile = "",
            FoundFolder = "";
        static void Main(string[] args)
        {
            List<string> StringList = initList(getFieldFromXML("settings.xml", "/Properties", "StringFile")),//Список строк поиска
                FolderList = initList(getFieldFromXML("settings.xml", "/Properties", "FolderFile"));//Список папок поиска
            ResultFile = getFieldFromXML("settings.xml", "/Properties", "ResultFile");//Файл с результатами поиска
            FoundFolder = getFieldFromXML("settings.xml", "/Properties", "FoundFolder");//Папка, куда будут копироваться файлы с найденными строками
            int ThreadCount = StringList.Count().ToString().Length;
            Console.WriteLine("{0}. Число потоков - {1}.", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), ThreadCount);
            Thread.Sleep(2000);
            List<Thread> ThreadList = new List<Thread>();
            if (!Directory.Exists(FoundFolder))
                Directory.CreateDirectory(FoundFolder);
            //Разделение строковых данных между потоками
            for (int i = 0; i < ThreadCount; i++)
            {
                List<string> InnerStringList = new List<string>();
                for (int j = i * StringList.Count() / ThreadCount; j < (i + 1) * StringList.Count() / ThreadCount; j++)
                    InnerStringList.Add(StringList.ElementAt(j));
                Thread InnerThread = new Thread(() =>
                {
                    foreach (string InnerFolder in FolderList)
                        checkFolder(InnerFolder, InnerStringList, i);
                }
                );
                //Запуск потока поиска
                InnerThread.Start();
                Console.WriteLine("{0}. Поток {1} запущен.", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), i);
                Thread.Sleep(100);
                ThreadList.Add(InnerThread);
            }
            foreach (Thread InnerThread in ThreadList)
                InnerThread.Join();
            Console.WriteLine("{0}. Поиск завершен.", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
        }
        static List<string> initList(string FileName)
        {
            return File.ReadAllLines(FileName).Where(n => n.Length > 0).ToList<string>();
        }
        static void checkFolder(string FolderName, List<string> StringList, int ThreadNumber)
        {
            //Проход по папкам внутри папки поиска
            foreach (string InnerFolder in Directory.GetDirectories(FolderName))
                checkFolder(InnerFolder, StringList, ThreadNumber);

            //Проход по текстовым файлам внутри папки поиска
            foreach (string InnerFile in Directory.GetFiles(FolderName))
                foreach (string StringToSearch in StringList)
                {
                    string FoundString = getStringFromFile(InnerFile, StringToSearch);
                    if (FoundString != null)
                    {
                        lock ("WriteToResultFile")
                            using (StreamWriter SW = new StreamWriter(ResultFile, true, Encoding.Default))
                            {
                                SW.WriteLine(FoundString);
                                Console.WriteLine("{0}. Поток {3}. {1} найдено в файле {2}.", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), FoundString.Split('\t')[0], FoundString.Split('\t')[1], ThreadNumber);
                            }
                        FileInfo FI = new FileInfo(FoundString.Split('\t')[1]);
                        try
                        {
                            File.Copy(FI.FullName, FoundFolder + (FoundFolder[FoundFolder.Length - 1] == '\\' ? "" : "\\") + FI.Name, true);
                        }
                        catch (Exception Ex)
                        {
                            Console.WriteLine("{0}. Ошибка копирования файла. {1}", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), Ex.Message);
                            Console.ReadKey();
                        }
                    }
                }
        }
        static string getStringFromFile (string FileName, string StringToSearch)
        {
            using (StreamReader SR = new StreamReader(FileName, Encoding.Default))
                while (true)
                {
                    string FileString = SR.ReadLine();
                    if (FileString == null)
                        break;
                    if (FileString.Contains(StringToSearch))
                        return StringToSearch + "\t" + FileName;
                }
            return null;
        }
        static string getFieldFromXML(string XMLFile, string Path, string Node)
        {
            string Result = "";
            using (StreamReader SR = new StreamReader(XMLFile, Encoding.Default))
            {
                try
                {
                    XmlDocument XDoc = new XmlDocument();
                    XDoc.Load(SR);
                    foreach (XmlNode XNode in XDoc.SelectNodes(Path))
                        Result = XNode[Node].InnerText;
                }
                catch (Exception Ex)
                {
                    Console.WriteLine("{0}. Ошибка чтения из файла настроек. {1}", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), Ex.Message);
                    Console.ReadKey(); 
                    return null;
                }
            }
            return Result;
        }
    }
}
