﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Windows.Media;
using SPCode.Utils;

namespace SPCode;

[Serializable]
public class OptionsControl
{
    public static int SVersion = 15;
    public bool Editor_AgressiveIndentation = true;
    public bool Editor_AutoCloseBrackets = true;
    public bool Editor_AutoCloseStringChars = true;
    public bool Editor_AutoSave;
    public int Editor_AutoSaveInterval = 5 * 60;
    public string Editor_FontFamily = "Consolas";
    public double Editor_FontSize = 16.0;
    public int Editor_IndentationSize = 4;
    public bool Editor_ReformatLineAfterSemicolon = true;
    public bool Editor_ReplaceTabsToWhitespace;
    public double Editor_ScrollLines = 4.0;
    public bool Editor_ShowSpaces;
    public bool Editor_ShowTabs;
    public bool Editor_TabToAutocomplete;

    public bool Editor_WordWrap = false;

    public string Language = string.Empty;

    public string[] LastOpenFiles = new string[0];

    public string Program_AccentColor = "Red";

    public bool Program_CheckForUpdates = true;

    public byte[] Program_CryptoKey;
    public bool Program_DiscordPresence = true;
    public bool Program_DiscordPresenceTime = true;
    public bool Program_DiscordPresenceFile = true;

    public bool Program_DynamicISAC = true;

    public string Program_ObjectBrowserDirectory = string.Empty;
    public double Program_ObjectbrowserWidth = 300.0;

    public bool Program_OpenCustomIncludes = false;
    public bool Program_OpenIncludesRecursively = false;

    public string Program_SelectedConfig = string.Empty;
    public string Program_Theme = "Dark";

    public bool Program_UseHardwareAcceleration = true;
    public bool Program_UseHardwareSalts = true;
    public SerializableColor SH_Chars = new(0xFF, 0xD6, 0x9C, 0x85);

    public SerializableColor SH_Comments = new(0xFF, 0x57, 0xA6, 0x49);
    public SerializableColor SH_CommentsMarker = new(0xFF, 0xFF, 0x20, 0x20);
    public SerializableColor SH_Constants = new(0xFF, 0xBC, 0x62, 0xC5);
    public SerializableColor SH_ContextKeywords = new(0xFF, 0x56, 0x9C, 0xD5);
    public SerializableColor SH_Deprecated = new(0xFF, 0xFF, 0x00, 0x00);
    public SerializableColor SH_Functions = new(0xFF, 0x56, 0x9C, 0xD5);

    public bool SH_HighlightDeprecateds = true;
    public SerializableColor SH_Keywords = new(0xFF, 0x56, 0x9C, 0xD5);
    public SerializableColor SH_Methods = new(0xFF, 0x3B, 0xC6, 0x7E);
    public SerializableColor SH_Numbers = new(0xFF, 0x97, 0x97, 0x97);
    public SerializableColor SH_PreProcessor = new(0xFF, 0x7E, 0x7E, 0x7E);
    public SerializableColor SH_SpecialCharacters = new(0xFF, 0x8F, 0x8F, 0x8F);
    public SerializableColor SH_Strings = new(0xFF, 0xF4, 0x6B, 0x6C);
    public SerializableColor SH_Types = new(0xFF, 0x28, 0x90, 0xB0); //56 9C D5
    public SerializableColor SH_TypesValues = new(0xFF, 0x56, 0x9C, 0xD5);
    public SerializableColor SH_UnkownFunctions = new(0xFF, 0x45, 0x85, 0xC5);

    public bool UI_Animations = true;
    public bool UI_ShowToolBar;

    // Version 12
    public LinkedList<string> RecentFiles = new();

    // Version 13
    public SearchOptions SearchOptions;

    // Version 14
    public ActionOnClose ActionOnClose;

    // Version 15
    public int TranslationsVersion;

    public int Version = 11;

    public void EnsureCompatibility()
    {
        // Prevent previous versions from loading old "Base" prefix nomenclature
        Program_Theme = Program_Theme.Replace("Base", string.Empty);
    }

    public void FillNullToDefaults()
    {
        if (Program_CryptoKey == null)
        {
            ReCreateCryptoKey();
        }

        if (SVersion > Version)
        {
            Program.ClearUpdateFiles();
            if (Version < 2)
            {
                UI_ShowToolBar = false;
            }

            if (Version < 3)
            {
                Editor_ReformatLineAfterSemicolon = true;
                Editor_ScrollLines = 4.0;
                Program_CheckForUpdates = true;
            }

            if (Version < 4)
            {
                Editor_ReplaceTabsToWhitespace = false;
            }

            if (Version < 5)
            {
                Program_DynamicISAC = true;
            }

            if (Version < 7)
            {
                Program_AccentColor = "Red";
                Program_Theme = "Dark";
                NormalizeSHColors();
            }

            if (Version < 8)
            {
                Editor_AutoCloseBrackets = true;
            }

            if (Version < 9)
            {
                Editor_AutoCloseStringChars = true;
                Editor_ShowSpaces = false;
                Editor_ShowTabs = false;
                Editor_IndentationSize = 4;
                Language = "";
                Program_ObjectBrowserDirectory = string.Empty;
                Program_ObjectbrowserWidth = 300.0;
                Editor_AutoSave = false;
                Editor_AutoSaveInterval = 5 * 60;
                ReCreateCryptoKey();
                Program.MakeRCCKAlert();
            }

            if (Version < 10)
            {
                Program_UseHardwareSalts = true;
            }

            if (Version < 11)
            {
                if (Program_AccentColor == "Cyan")
                {
                    Program_AccentColor = "Blue";
                }
            }
            if (Version < 12)
            {
                Editor_TabToAutocomplete = false;
            }
            if (Version < 13)
            {
                SearchOptions.FindText = "";
                SearchOptions.ReplaceText = "";
                SearchOptions.SearchType = 0;
                SearchOptions.Document = 0;
                SearchOptions.CaseSensitive = false;
                SearchOptions.MultilineRegex = false;
                SearchOptions.ReplaceType = 0;
            }
            if (Version < 14)
            {
                TranslationsVersion = 0;
            }

            //new Optionsversion - reset new fields to default
            Version = SVersion; //then Update Version afterwards
        }
    }

    public void ReCreateCryptoKey()
    {
        var buffer = RandomNumberGenerator.GetBytes(16);;
        Program_CryptoKey = buffer;
    }

    public void NormalizeSHColors()
    {
        SH_Comments = new SerializableColor(0xFF, 0x57, 0xA6, 0x49);
        SH_CommentsMarker = new SerializableColor(0xFF, 0xFF, 0x20, 0x20);
        SH_Strings = new SerializableColor(0xFF, 0xF4, 0x6B, 0x6C);
        SH_PreProcessor = new SerializableColor(0xFF, 0x7E, 0x7E, 0x7E);
        SH_Types = new SerializableColor(0xFF, 0x28, 0x90, 0xB0);
        SH_TypesValues = new SerializableColor(0xFF, 0x56, 0x9C, 0xD5);
        SH_Keywords = new SerializableColor(0xFF, 0x56, 0x9C, 0xD5);
        SH_ContextKeywords = new SerializableColor(0xFF, 0x56, 0x9C, 0xD5);
        SH_Chars = new SerializableColor(0xFF, 0xD6, 0x9C, 0x85);
        SH_UnkownFunctions = new SerializableColor(0xFF, 0x45, 0x85, 0xC5);
        SH_Numbers = new SerializableColor(0xFF, 0x97, 0x97, 0x97);
        SH_SpecialCharacters = new SerializableColor(0xFF, 0x8F, 0x8F, 0x8F);
        SH_Deprecated = new SerializableColor(0xFF, 0xFF, 0x00, 0x00);
        SH_Constants = new SerializableColor(0xFF, 0xBC, 0x62, 0xC5);
        SH_Functions = new SerializableColor(0xFF, 0x56, 0x9C, 0xD5);
        SH_Methods = new SerializableColor(0xFF, 0x3B, 0xC6, 0x7E);
    }

    public static void Save()
    {
        if (Program.PreventOptionsSaving)
        {
            return;
        }

        try
        {
            var formatter = new BinaryFormatter();
            using var fileStream = new FileStream(PathsHelper.OptionsFilePath, FileMode.Create, FileAccess.ReadWrite,
                FileShare.None);
            formatter.Serialize(fileStream, Program.OptionsObject);
        }
        catch (Exception)
        {
        }
    }

    public static OptionsControl Load(out bool ProgramIsNew)
    {
        try
        {
            if (File.Exists(PathsHelper.OptionsFilePath))
            {
                OptionsControl optionsObject;
                var formatter = new BinaryFormatter();
                using (var fileStream = new FileStream(PathsHelper.OptionsFilePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    optionsObject = (OptionsControl)formatter.Deserialize(fileStream);
                }

                optionsObject.FillNullToDefaults();
                optionsObject.EnsureCompatibility();
                ProgramIsNew = false;
                return optionsObject;
            }
        }
        catch (Exception)
        {

        }

        var oco = new OptionsControl();
        oco.ReCreateCryptoKey();
#if DEBUG
        ProgramIsNew = false;
#else
        ProgramIsNew = true;
#endif
        return oco;
    }
}

[Serializable]
public class SerializableColor
{
    public byte A;
    public byte B;
    public byte G;
    public byte R;

    public SerializableColor(byte _A, byte _R, byte _G, byte _B)
    {
        A = _A;
        R = _R;
        G = _G;
        B = _B;
    }

    public static implicit operator SerializableColor(Color c)
    {
        return new SerializableColor(c.A, c.R, c.G, c.B);
    }

    public static implicit operator Color(SerializableColor c)
    {
        return Color.FromArgb(c.A, c.R, c.G, c.B);
    }
}

[Serializable]
public struct SearchOptions
{
    public string FindText;
    public string ReplaceText;
    public int SearchType;
    public int Document;
    public bool CaseSensitive;
    public bool MultilineRegex;
    public int ReplaceType;
}

[Serializable]
public enum ActionOnClose
{
    Prompt,
    Save,
    DontSave
}