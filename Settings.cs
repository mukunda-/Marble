// Marbles
// (C) 2020 Mukunda Johnson
/////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

/////////////////////////////////////////////////////////////////////////////////////////
namespace Marbles
{
    //-----------------------------------------------------------------------------------
    // Manages global program settings and synchronizing them to disk in JSON format.
    public class Settings
    {
        private readonly string name = "settings";
        private readonly FileSystemWatcher settingsWatcher;

        // To ignore reloading configuration when the app is changing the settings file.
        // Also used to prevent some "bounce trigger" issues that the filesystem watcher
        //  has.
        private DateTime ignoreFileChangesAt = DateTime.MinValue;

        public delegate void SettingsHandler(Settings settings);
        public event SettingsHandler Loaded;

        private readonly DelayedCall loadCaller;

        public class Fields
        {
            // Using strings for these two so it keeps the text boxes set as is.
            public string SprintTime               { get; set; } = "25";
            public string RestTime                 { get; set; } = "5";
            public int    MarblesDoneToday         { get; set; } = 0;
            public string DateToday                { get; set; } = "";
            public bool   ShowSprintBadge          { get; set; } = true;
            public bool   ShowRestBadge            { get; set; } = true;
            public bool   MinimizeWhenSprintStarts { get; set; } = false;
            public bool   PopupWhenRestStarts      { get; set; } = false;
            public bool   ColorTaskbarDuringSprint { get; set; } = true;
            public bool   ColorTaskbarDuringRest   { get; set; } = true;
            public bool   ShowYellowFlashAfterRest { get; set; } = true;
            public bool   WindowAlwaysOnTop        { get; set; } = false;
        };

        public Fields fields = null;

        //-------------------------------------------------------------------------------
        public Settings()
        {
            loadCaller = new DelayedCall();
            Directory.CreateDirectory(AppDataFolder);
            Load();

            settingsWatcher = new FileSystemWatcher
            {
                NotifyFilter = NotifyFilters.LastWrite,
                Path = AppDataFolder,
                Filter = name + ".json"
            };
            settingsWatcher.Changed += (sender, e) =>
            {
                if (Application.Current == null) return;
                if (e.ChangeType != WatcherChangeTypes.Changed) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if ((DateTime.Now - ignoreFileChangesAt).TotalMilliseconds < 100)
                    {
                        return;
                    }
                    ignoreFileChangesAt = DateTime.Now;
                    QueueLoad(250);
                });
            };
            settingsWatcher.EnableRaisingEvents = true;
        }

        //-------------------------------------------------------------------------------
        // Returns the path to User/AppData/Roaming/Marbles.
        public static string AppDataFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "Marbles");
            }
        }

        //-------------------------------------------------------------------------------
        // Returns the path to User/AppData/Roaming/Marbles/settings.json
        public string SettingsFilePath
        {
            get
            {
                return Path.Combine(AppDataFolder, this.name + ".json");
            }
        }

        //-------------------------------------------------------------------------------
        // Opens the default text editor with the settings json file.
        public void OpenEditor()
        {
            Save();
            Process.Start(SettingsFilePath);
        }

        //-------------------------------------------------------------------------------
        // Queues a settings load operation. The delay is meant to allow the computer
        //  to finish writing the settings file before we read from it. If an IO error
        //  occurs we retry after 1000ms.
        public void QueueLoad(int delayMs = 100)
        {
            loadCaller.Call(Load, delayMs);
        }
        
        //-------------------------------------------------------------------------------
        // Load or reload the settings from disk.
        public void Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    // Allow JSON comments (even though they would be overwritten when
                    //  we save).
                    // Basically, we want to be very permissive here. Strictness is only
                    //  annoying for the user.
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    // Same with trailing commas and case insensitivity.
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = false,
                };

                try
                {
                    string json;
                    try
                    {
                        json = File.ReadAllText(SettingsFilePath);
                    }
                    catch (IOException)
                    {
                        // IO error (like they are still writing the file). Wait and try again.
                        QueueLoad(1000);
                        return;
                    }
                    this.fields = System.Text.Json.JsonSerializer.Deserialize<Fields>(json, options);
                }
                catch
                {
                    // Serialization error.
                }

                // If we make it to here and it's null, this is the first time we're
                //  running or something went wrong.
                if (this.fields == null) this.fields = new Fields();

                // Sanitization here. Most is handled by JSON format parsing.
                if (this.fields.MarblesDoneToday < 0) this.fields.MarblesDoneToday = 0;
            }
            else
            {
                // Will use default values.
                this.fields = new Fields();
                Save();
            }
            
            Loaded?.Invoke(this);
        }

        //-------------------------------------------------------------------------------
        public void Save()
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                // Pretty-print so it can be edited easily by humies.
                WriteIndented = true,
            };
            string text = System.Text.Json.JsonSerializer.Serialize(fields, options);

            ignoreFileChangesAt = DateTime.Now;
            try
            {
                File.WriteAllText(SettingsFilePath, text);
            }
            catch (IOException)
            {
                // Couldn't save settings. Probably should tell someone about this.
                // Oh well...
            }
        }
    }
}
/////////////////////////////////////////////////////////////////////////////////////////
