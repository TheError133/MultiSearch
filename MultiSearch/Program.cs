using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Threading;

namespace MultiSearch
{
    class Program
    {
        static string ResultFile = "";
        static void Main(string[] args)
        {
            List<string> StringList = StringList = initList(getFieldFromXML("settings.xml", "/Properties", "StringFile")),
                FolderList = initList(getFieldFromXML("settings.xml", "/Properties", "FolderFile"));
            ResultFile = getFieldFromXML("settings.xml", "/Properties", "ResultFile");
        }
        static List<string> initList(string FileName)
        {
            return File.ReadAllLines(FileName).ToList<string>();
        }
        static void checkFolder(string FolderName, List<string> StringList)
        {
            //Проход по папкам внутри папки поиска
            foreach (string InnerFolder in Directory.GetDirectories(FolderName))
                checkFolder(InnerFolder, StringList);

            //Проход по текстовым файлам внутри папки поиска
            foreach (string InnerFile in Directory.GetFiles(FolderName))
                foreach (string StringToSearch in StringList)
                {
                    string FoundString = getStringFromFile(InnerFile, StringToSearch);
                    if (FoundString != null)
                        lock ("WriteToResultFile")
                            using (StreamWriter SW = new StreamWriter(ResultFile, true, Encoding.Default))
                                SW.WriteLine(FoundString);
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
                    return null;
                }
            }
            return Result;
        }
    }
}
