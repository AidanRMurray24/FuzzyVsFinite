﻿using UnityEngine;
using System.IO;

public static class CSVManager
{
    private static string reportDirectoryName = "Report";
    private static string reportFileName = "report.csv";
    private static string reportSeparator = ",";
    private static string[] reportHeaders = new string[15]
    {
        "Round Number",
        "Agent That Won",
        "Time Taken(s)",
        "Fuzzy Bullets Fired",
        "Fuzzy Bullets Hit",
        "Finite Bullets Fired",
        "Finite Bullets Hit",
        "Fuzzy Move To Target Count",
        "Fuzzy HidingCount",
        "Fuzzy Shooting Count",
        "Fuzzy Reloading Count",
        "Finite Move To Target Count",
        "Finite HidingCount",
        "Finite Shooting Count",
        "Finite Reloading Count"
    };
    private static string timeStampHeader = "Time Stamp";

    #region Interations

	public static void AppendToReport(string[] strings)
    {
        VerifyDirectory();
        VerifyFile();
        using (StreamWriter sw = File.AppendText(GetFilePath()))
        {
            string finalString = "";
            for (int i = 0; i < strings.Length; i++)
            {
                if (finalString != "")
                {
                    finalString += reportSeparator;
                }
                finalString += strings[i];
            }
            finalString += reportSeparator + GetTimeStamp();
            sw.WriteLine(finalString);
        }
    }

    public static void CreateReport()
    {
        VerifyDirectory();
        using (StreamWriter sw = File.CreateText(GetFilePath()))
        {
            string finalString = "";
            for (int i = 0; i < reportHeaders.Length; i++)
            {
                if (finalString != "")
                {
                    finalString += reportSeparator;
                }
                finalString += reportHeaders[i];
            }
            finalString += reportSeparator + timeStampHeader;
            sw.WriteLine(finalString);
        }
    }

    #endregion

    #region Operations

    static void VerifyDirectory()
    {
        string dir = GetDirectoryPath();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    static void VerifyFile()
    {
        string file = GetFilePath();
        if (!File.Exists(file))
        {
            CreateReport();
        }
    }

	#endregion

	#region Queries

	static string GetDirectoryPath()
    {
        return Application.dataPath + "/" + reportDirectoryName;
    }

    static string GetFilePath()
    {
        return GetDirectoryPath() + "/" + reportFileName;
    }

    static string GetTimeStamp()
    {
        return System.DateTime.UtcNow.ToString();
    }

	#endregion
}
