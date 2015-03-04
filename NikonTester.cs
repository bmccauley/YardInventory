using System;
using CIDARMTWrapper;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Web.UI;
using System.Collections.Generic;

namespace YardInventory
{
    class NikonTester
    {
        static void Main()
        {
            MTInterface.SetConfiguration(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new int[0], 1, 0, 0, 0);
            string[] files = Directory.GetFiles(@"C:\Users\bmccauley.PCI\Pictures\11-21-2014\Samsung0MPHCroppedDuplicatesBW");
            string[,] grid = new string[files.Length + 1, 16];
            StreamReader reader = new StreamReader(@"C:\Users\bmccauley.PCI\Documents\CroppedMasterIDs.csv");
            Dictionary<string, string> fileDict = new Dictionary<string, string>();
            string line = reader.ReadLine();
            while (line != null)
            {
                string[] lineArray = line.Split(new Char[] {','});
                fileDict.Add(lineArray[0], lineArray[1]);
                line = reader.ReadLine();
            }
            reader.Close();
            grid[0, 0] = "File Name";
            grid[0, 1] = "True Container ID";
            grid[0, 2] = "Generated Container ID";
            for (int i = 0; i < files.Length; i++)
            {
                grid[i + 1, 0] = files[i];
                grid[i + 1, 1] = fileDict[files[i]];
            }
            string[] results = new string[files.Length];
            int[] heights = { 40, 45, 50, 55, 60, 65 };
            for (int index = 0; index < heights.Length; index++)
            {
                int i = heights[index];
                grid[0, index + 2] = i + " pixels";
                ProcessImagesForCharacterHeight(files, results, i);
                for (int j = 0; j < results.Length; j++)
                {
                    string result = results[j];
                    grid[j + 1, index + 2] = result;
                }
            }
            grid[0, heights.Length + 2] = "Match";
            grid[0, heights.Length + 3] = "False -";
            grid[0, heights.Length + 4] = "Prefix";
            grid[0, heights.Length + 5] = "Suffix";
            grid[0, heights.Length + 6] = "Both";
            int matchCount = 0;
            HashSet<string> fullContainerSet = new HashSet<string>();
            HashSet<string> foundContainerSet = new HashSet<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string trueID = fileDict[grid[i + 1, 0]];
                fullContainerSet.Add(trueID);
                int rowMatchCount = 0;
                int falseNegativeCount = 0;
                int prefixCount = 0;
                int suffixCount = 0;
                int bothCount = 0;
                for (int j = 0; j < heights.Length; j++)
                {
                    string candidateID = grid[i + 1, j + 2];
                    if (trueID.Equals(candidateID))
                    {
                        foundContainerSet.Add(trueID);
                        rowMatchCount++;
                    }
                    else if (candidateID.Equals("NO CODE FO") || candidateID.Equals("?"))
                    {
                        falseNegativeCount++;
                    }
                    else
                    {
                        string prefix = candidateID.Substring(0, 4);
                        string suffix = candidateID.TrimStart(prefix.ToCharArray());
                        string truePrefix = trueID.Substring(0, 4);
                        string trueSuffix = trueID.TrimStart(truePrefix.ToCharArray());
                        if (!prefix.Equals(truePrefix) && !suffix.Equals(trueSuffix))
                        {
                            bothCount++;
                        }
                        else if (!prefix.Equals(truePrefix))
                        {
                            prefixCount++;
                        }
                        else if (!suffix.Equals(trueSuffix))
                        {
                            suffixCount++;
                        }
                    }
                }
                if (rowMatchCount > 0)
                {
                    matchCount++;
                }
                grid[i + 1, heights.Length + 2] = rowMatchCount + "";
                grid[i + 1, heights.Length + 3] = falseNegativeCount + "";
                grid[i + 1, heights.Length + 4] = prefixCount + "";
                grid[i + 1, heights.Length + 5] = suffixCount + "";
                grid[i + 1, heights.Length + 6] = bothCount + "";
            }
            int matchPercentage = 100 * matchCount / files.Length;
            grid[0, heights.Length + 8] = "Image Match Rate";
            grid[0, heights.Length + 9] = matchPercentage + "%";
            matchPercentage = 100 * foundContainerSet.Count / fullContainerSet.Count;
            grid[1, heights.Length + 8] = "Container Match Rate";
            grid[1, heights.Length + 9] = matchPercentage + "%";
            StreamWriter writer = new StreamWriter(@"C:\Users\bmccauley.PCI\Documents\OCROutput.csv");
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                StringBuilder lineBuilder = new StringBuilder();
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    lineBuilder.Append(grid[i, j]).Append(",");
                }
                writer.WriteLine(lineBuilder.ToString());
            }
            writer.Close();
        }

        public static void ProcessImagesForCharacterHeight(string[] files, string[] results, int height)
        {
            MTInterface.Init(height, 0, 0, false);
            foreach (string i in files)
            {
                String info = GetFileNameAndCoordinates(i);
                MTInterface.Add(i, info, false);
            }
            MTInterface.QueryEnd();
            NLInfo element = MTInterface.GetFirstElement;
            int j = 0;
            while (element != null)
            {
                CodeInfo code = element.GetFirstItem;
                while (code != null)
                {
                    String codeString = code.GetCodeNumber;
                    if (codeString.Length > 10)
                    {
                        codeString = codeString.Substring(0, 10);
                    }
                    float confidence = code.GetGlobalConfidence;
                    if (confidence > 0 && confidence < 70)
                    {
                        codeString = "?";
                    }
                    results[j] = codeString;
                    j++;
                    code = element.GetFirstItem;
                }
                element = MTInterface.GetFirstElement;
            }
        }

        public static string GetLatitude(Image image)
        {
            return GetCoordinate(image, 0x0002);
        }

        public static string GetLongitude(Image image)
        {
            return GetCoordinate(image, 0x0004);
        }

        public static string GetCoordinate(Image image, int propertyNumber)
        {
            PropertyItem propItem = image.GetPropertyItem(propertyNumber);
            uint degreesNumerator = BitConverter.ToUInt32(propItem.Value, 0);
            uint degreesDenominator = BitConverter.ToUInt32(propItem.Value, 4);
            uint minutesNumerator = BitConverter.ToUInt32(propItem.Value, 8);
            uint minutesDenominator = BitConverter.ToUInt32(propItem.Value, 12);
            uint secondsNumerator = BitConverter.ToUInt32(propItem.Value, 16);
            uint secondsDenominator = BitConverter.ToUInt32(propItem.Value, 20);
            uint degrees = degreesNumerator / degreesDenominator;
            uint minutes = minutesNumerator / minutesDenominator;
            uint seconds = secondsNumerator / secondsDenominator;
            StringBuilder coordinate = new StringBuilder("");
            coordinate.Append(degrees).Append(".").Append(pad(minutes)).Append(".").Append(pad(seconds));
            return coordinate.ToString();
        }

        public static string pad(uint number)
        {
            if (number < 10)
            {
                return "0" + number;
            }
            else
            {
                return "" + number;
            }
        }

        public static string GetFileNameAndCoordinates(string fileName)
        {
            StringBuilder info = new StringBuilder("");
            if (fileName.EndsWith(".jpg") || fileName.EndsWith(".JPG"))
            {
                Image image = Image.FromFile(fileName);
                info.Append(fileName).Append(" ").Append(GetLatitude(image)).Append(" N ").Append(GetLongitude(image)).Append(" W");
                image.Dispose();
            }
            return info.ToString();
        }
    }
}
