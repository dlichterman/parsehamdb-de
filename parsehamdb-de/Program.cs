using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using org.apache.pdfbox.pdmodel;
using org.apache.pdfbox.util;
using System.Text.RegularExpressions;
using System.Net;
using System.Configuration;
using System.IO;

namespace parsehamdb_de
{
    class Program
    {
        static void Main(string[] args)
        {
            //parse args
            string fileName = "out.csv";
            string rawFilename = "";
            string URL = "";
            bool writeRaw = false;
            bool deleteFile = true;
            if (args.Count() > 0)
            {
                if (args[0] == "?")
                {
                    Console.WriteLine("Optional parameters");
                    Console.WriteLine("-f [filename] to specify output filename");
                    Console.WriteLine("-r [filename] to output raw stripped PDF for debugging purposes");
                    Console.WriteLine("-url [url] to override config file download URL");
                    Console.WriteLine("-d Do not delete PDF after processing(default to delete)");
                    Environment.Exit(0);
                }
                else
                {
                    for (int i = 0; i < args.Count(); i++)
                    {
                        switch(args[i])
                        {
                            case "-f":
                                fileName = args[i+1];
                                break;
                            case "-r":
                                writeRaw = true;
                                rawFilename = args[i + 1];
                                break;
                            case "-url":
                                URL = args[i + 1];
                                break;
                            case "-d":
                                deleteFile = false;
                                break;

                        } 
                        
                    }
                }
                
            }
                   
            ProcessFile(fileName, writeRaw, rawFilename,URL, deleteFile);

        }

        static public void ProcessFile(string fileName,bool writeRaw,string rawFilename,string URL, bool deleteFile)
        {

            WebClient wc = new WebClient();
            try

            {
                Console.WriteLine("Downloading PDF");
                File.Delete("temp.pdf");
                if (URL != "")
                {
                    wc.DownloadFile(URL, "temp.pdf");
                }
                else
                {
                    wc.DownloadFile(ConfigurationManager.AppSettings["URL"], "temp.pdf");
                }
                Console.WriteLine("PDF downloaded");
            }
            catch(Exception e)
            {
                //
                Console.WriteLine("Error downloading: " + e.Message);
                Environment.Exit(-1);
            }
            
            PDDocument doc = null;
            string rawpdf;
            try
            {
                doc = PDDocument.load("temp.pdf");
                PDFTextStripper stripper = new PDFTextStripper();
                rawpdf = stripper.getText(doc);
                if (writeRaw)
                {
                    System.IO.File.WriteAllText(rawFilename, rawpdf);
                }
                Console.WriteLine("PDF imported");
            }
            finally
            {
                if (doc != null)
                {
                    doc.close();
                }
            }

            Regex rCall = new Regex(@"[D][A-Z][\d][A-Z]{1,3},");
            Regex rPage = new Regex(@"[ ][S][e][i][t][e][ ][\d]+[ ]");
            //string reg = @"[D][A-Z][\d][A-Z]{1,3},";

            List<string> callsigns = new List<string>();
            List<string> callsignsOut = new List<string>();

            callsigns.Add("FIRSTLINE");

            foreach (string s in rawpdf.Split(new[] { "\n" }, StringSplitOptions.None))
            {
                if (rCall.IsMatch(s))
                {
                    callsigns.Add(s.Trim());
                }
                else if (rPage.IsMatch(s))
                {
                    //do nothing - page #!
                }
                else
                {
                    callsigns[callsigns.Count - 1] = callsigns[callsigns.Count - 1] + " " + s.Trim();
                }
            }

            callsigns.RemoveAt(0);

            Console.WriteLine("Cleanup complete");

            Regex rParseCall = new Regex(@"([D][A-Z][\d][A-Z]{1,3}),[ ]([A|E]),[ ](.*)");
            Regex rParsePostal = new Regex(@"([\d]{5})[ ]([A-zÀ-ÿ]*[ ]*[A-zÀ-ÿ]*)");


            foreach (string s in callsigns)
            {
                
                Match m = rParseCall.Match(s);

                if (m.Groups[3].Value.Contains(";") || m.Groups[3].Value.Contains(",")) //Try to parse out address
                {
                    string[] rec = m.Groups[3].Value.Split(new char[] { ',', ';' });
                    string callLine = "";

                    if (rec.Count() == 2)
                    {
                        callLine = m.Groups[1].Value + "|" + m.Groups[2].Value + "|" + rec[0].TrimStart(' ') + "||";
                        Match m2 = rParsePostal.Match(rec[1]);
                        callLine += m2.Groups[1] + "|" + m2.Groups[2]; 
                    }
                    else
                    {
                        callLine = m.Groups[1].Value + "|" + m.Groups[2].Value + "|" + rec[0].TrimStart(' ') + "|" + rec[1].TrimStart(' ') + "|";
                        Match m2 = rParsePostal.Match(rec[2]);
                        callLine += m2.Groups[1] + "|" + m2.Groups[2];
                    }

                    callsignsOut.Add(callLine);
                }
                else
                {
                    callsignsOut.Add(m.Groups[1].Value + "|" + m.Groups[2].Value + "|" + m.Groups[3].Value + "|||");
                }
            }

            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName,false);
            foreach(string s in callsignsOut)
            {
                file.WriteLine(s.Replace("\r",""));
            }
            
            file.Close();
            if (deleteFile)
            {
                File.Delete("temp.pdf");
            }
        }
    }
}
