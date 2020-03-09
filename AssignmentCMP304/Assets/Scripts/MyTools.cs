using UnityEngine;
using UnityEditor;

public static class MyTools
{
    [MenuItem("My Tools/1. Reset Report %12")]
    static void DEV_ResetReport()
    {
        CSVManager.CreateReport();
        EditorApplication.Beep();
        Debug.Log("<color=orange>The report has been reset!</color>");
    }
}
