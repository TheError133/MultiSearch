using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using SharpCompress.Archive;
using SharpCompress.Common;

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
            int ThreadCount = StringList.Count().ToString().Length;//Количество потоков утилиты. Зависит от числа символов в количестве строк поиска (5 - 1 поток, 16 - 2 потока, 291 - 3 потока, 10927 - 5 потоков и т.д.)
            Console.WriteLine("{0}. Число потоков - {1}.", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), ThreadCount);
            Thread.Sleep(2000);
            List<Thread> ThreadList = new List<Thread>();
            //При отсутствии папка результатов создается
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
                        checkFolder(InnerFolder, InnerStringList, i, InnerFolder);
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
            Thread.Sleep(2000);
        }

        //Рекурсивный поиск по файлам в папках
        static void checkFolder(string FolderName, List<string> StringList, int ThreadNumber, string FoundFilePath)
        {
            Console.WriteLine("{0}. Поток {1}. Анализируем данные в {2}",DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), ThreadNumber, FoundFilePath);
            //Проход по папкам внутри папки поиска
            foreach (string InnerFolder in Directory.GetDirectories(FolderName))
                checkFolder(InnerFolder, StringList, ThreadNumber, trimSeparator(FoundFilePath) + "\\" + (new DirectoryInfo(InnerFolder)).Name);

            foreach (string InnerFile in Directory.GetFiles(FolderName))
            {
                //Проход по текстовым файлам внутри папки поиска
                foreach (string StringToSearch in StringList)
                {
                    string FoundString = getStringFromFile(InnerFile, StringToSearch, FoundFilePath);
                    if (FoundString != null)
                    {
                        lock ("WriteToResultFile")
                            using (StreamWriter SW = new StreamWriter(ResultFile, true, Encoding.Default))
                            {
                                SW.WriteLine(FoundString);
                                Console.WriteLine("{0}. Поток {3}. {1} найдено в файле {2}.", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), FoundString.Split('\t')[0], FoundString.Split('\t')[1], ThreadNumber);
                            }
                        FileInfo FI = new FileInfo(InnerFile);
                        try
                        {
                            File.Copy(FI.FullName, trimSeparator(FoundFolder) + "\\" + FI.Name, true);
                        }
                        catch (Exception Ex)
                        {
                            Console.WriteLine("{0}. Ошибка копирования файла. {1}", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), Ex.Message);
                            Console.ReadKey();
                        }
                    }
                }
                /*
                //Проверка файлов как zip-архивов
                if (getFieldFromXML("settings.xml", "/Properties", "SearchInZip").ToLower() == "true")
                {
                    string NewWorkingFolder = trimSeparator(Directory.GetCurrentDirectory()) + "\\" + "Thread_" + ThreadNumber + "_" + DateTime.Now.ToString("ddMMyyyy_HHmmssfff");
                    //Console.WriteLine("Создаваемая временная папка - {0}", NewWorkingFolder);
                    //Console.ReadKey();
                    try
                    {
                        using (ZipFile ZFile = new ZipFile(InnerFile))
                        {
                            if (!Directory.Exists(NewWorkingFolder))
                                Directory.CreateDirectory(NewWorkingFolder);
                            ZFile.ExtractAll(NewWorkingFolder, ExtractExistingFileAction.OverwriteSilently);
                            FileInfo FI = new FileInfo(InnerFile);
                            checkFolder(NewWorkingFolder, StringList, ThreadNumber, trimSeparator(FoundFilePath) + "\\" + FI.Name);
                            if (Directory.Exists(NewWorkingFolder))
                                Directory.Delete(NewWorkingFolder, true);
                        }
                    }
                    catch (Exception Ex)
                    {
                        //Файл не является zip-архивом
                    }
                }
                */
                //Проверка файлов как архивов
                if (getFieldFromXML("settings.xml", "/Properties", "SearchInArchives").ToLower() == "true")
                {
                    string NewWorkingFolder = trimSeparator(Directory.GetCurrentDirectory()) + "\\" + "Thread_" + ThreadNumber + "_" + DateTime.Now.ToString("ddMMyyyy_HHmmssfff");
                    try
                    {
                        if (!Directory.Exists(NewWorkingFolder))
                            Directory.CreateDirectory(NewWorkingFolder);
                        var Archive = ArchiveFactory.Open(InnerFile);
                        foreach (var Entry in Archive.Entries)
                            Entry.WriteToDirectory(NewWorkingFolder, ExtractOptions.Overwrite);
                        FileInfo FI = new FileInfo(InnerFile);
                        checkFolder(NewWorkingFolder, StringList, ThreadNumber, trimSeparator(FoundFilePath) + "\\" + FI.Name);
                        if (Directory.Exists(NewWorkingFolder))
                            Directory.Delete(NewWorkingFolder, true);
                    }
                    catch (Exception Ex)
                    {
                        //Файл не является rar-архивом
                        if (Directory.Exists(NewWorkingFolder))
                            Directory.Delete(NewWorkingFolder, true);
                    }
                }
            }
        }

        //Прострочная выгрузка файла в список
        static List<string> initList(string FileName)
        {
            return File.ReadAllLines(FileName, Encoding.Default).Where(n => n.Length > 0).ToList<string>();
        }

        //Поиск строки в файле
        static string getStringFromFile(string FileName, string StringToSearch, string FoundFilePath)
        {
            using (StreamReader SR = new StreamReader(FileName, Encoding.Default))
                while (true)
                {
                    string FileString = SR.ReadLine();
                    FileInfo FI = new FileInfo(FileName);
                    if (FileString == null)
                        break;
                    if (FileString.Contains(StringToSearch))
                        return StringToSearch + "\t" + trimSeparator(FoundFilePath) + "\\" + FI.Name;
                }
            return null;
        }

        //Считывание поля из файла XML
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

        //Обрезалка слеша на конце строки с именем папки
        static string trimSeparator (string InputString)
        {
            return InputString[InputString.Length - 1] == '\\' ? InputString.Substring(0, InputString.Length - 1) : InputString;
        }
    }
}
