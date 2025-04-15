#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Reporting;

public static class GoogleSheetsUtils
{
    static void PushExtension(GoogleSheetsExtension googleExtension)
    {
        // Setup the connection to Google
        var googleSheets = new GoogleSheets(googleExtension.SheetsServiceProvider)
        {
            SpreadSheetId = googleExtension.SpreadsheetId
        };

        // Now send the update. We can pass in an optional ProgressBarReporter so that we can updates in the Editor.
        googleSheets.PushStringTableCollection(googleExtension.SheetId,
            googleExtension.TargetCollection as StringTableCollection, googleExtension.Columns,
            new ProgressBarReporter());
    }

    static void PullExtension(GoogleSheetsExtension googleExtension)
    {
        // Setup the connection to Google
        var googleSheets = new GoogleSheets(googleExtension.SheetsServiceProvider)
        {
            SpreadSheetId = googleExtension.SpreadsheetId
        };

        // Now update the collection. We can pass in an optional ProgressBarReporter so that we can updates in the Editor.
        googleSheets.PullIntoStringTableCollection(googleExtension.SheetId,
            googleExtension.TargetCollection as StringTableCollection, googleExtension.Columns,
            googleExtension.RemoveMissingPulledKeys, new ProgressBarReporter());
    }

    /// <summary>
    /// This example shows how we can push every String Table Collection that contains a Google Sheets extension.
    /// </summary>
    /// Temporarily disable MenuItem to prevent unintentionally clicking and pushing local data
    // [MenuItem("Localization/Google Sheets/Push All Google Sheets Extensions")]
    public static void PushAllExtensions()
    {
        // Get every String Table Collection
        var stringTableCollections = LocalizationEditorSettings.GetStringTableCollections();

        foreach (var collection in stringTableCollections)
            // Its possible a String Table Collection may have more than one GoogleSheetsExtension.
            // For example if each Locale we pushed/pulled from a different sheet.
        foreach (var extension in collection.Extensions)
            if (extension is GoogleSheetsExtension googleExtension)
                PushExtension(googleExtension);
    }

    /// <summary>
    /// This example shows how we can push every String Table Collection that contains a Google Sheets extension.
    /// </summary>
    [MenuItem("Localization/Google Sheets/Pull All Google Sheets Extensions")]
    public static void PullAllExtensions()
    {
        // Get every String Table Collection
        var stringTableCollections = LocalizationEditorSettings.GetStringTableCollections();

        foreach (var collection in stringTableCollections)
            // Its possible a String Table Collection may have more than one GoogleSheetsExtension.
            // For example if each Locale we pushed/pulled from a different sheet.
        foreach (var extension in collection.Extensions)
            if (extension is GoogleSheetsExtension googleExtension)
                PullExtension(googleExtension);
    }
}
#endif
