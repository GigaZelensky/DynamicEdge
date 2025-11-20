using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DynamicEdge
{
    [DataContract]
    public class AppSettings
    {
        [DataMember] public float MaxHealth { get; set; }
        [DataMember] public float RegenRate { get; set; }
        [DataMember] public float DamageMultiplier { get; set; }
        [DataMember] public int BreakThreshold { get; set; }
        [DataMember] public int CooldownFrames { get; set; }
        [DataMember] public int PollRateActive { get; set; }
        [DataMember] public int PollRateIdle { get; set; }
        [DataMember] public int EdgeProximityPx { get; set; }
        [DataMember] public int IdleResetDistance { get; set; }
        [DataMember] public float SpeedEaseMultiplier { get; set; }

        public AppSettings()
        {
            MaxHealth = 100f;
            RegenRate = 15f;
            DamageMultiplier = 0.6f;
            BreakThreshold = 70;
            CooldownFrames = 25;
            PollRateActive = 10;
            PollRateIdle = 200;
            EdgeProximityPx = 2;
            IdleResetDistance = 50;
            SpeedEaseMultiplier = 0.01f;
        }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        public void Clamp()
        {
            MaxHealth = Math.Max(1f, MaxHealth);
            RegenRate = Math.Max(0f, RegenRate);
            DamageMultiplier = Math.Max(0f, DamageMultiplier);
            BreakThreshold = Math.Max(1, Math.Min(BreakThreshold, 1000));
            CooldownFrames = Math.Max(1, Math.Min(CooldownFrames, 500));
            PollRateActive = Math.Max(1, Math.Min(PollRateActive, 1000));
            PollRateIdle = Math.Max(1, Math.Min(PollRateIdle, 5000));
            EdgeProximityPx = Math.Max(1, Math.Min(EdgeProximityPx, 10));
            IdleResetDistance = Math.Max(5, Math.Min(IdleResetDistance, 1000));
            SpeedEaseMultiplier = Math.Max(0f, Math.Min(SpeedEaseMultiplier, 1f));
        }

        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }
    }

    public static class AppSettingsStore
    {
        public static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicEdge");

        public static readonly string ConfigPath = Path.Combine(ConfigDirectory, "settings.json");

        public static AppSettings LoadOrDefault()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    using (FileStream fs = File.OpenRead(ConfigPath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                        var loaded = serializer.ReadObject(fs) as AppSettings;
                        if (loaded != null)
                        {
                            loaded.Clamp();
                            return loaded;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load settings from " + ConfigPath + "; using defaults", ex);
            }

            return AppSettings.CreateDefault();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                if (settings == null) return;
                settings.Clamp();
                Directory.CreateDirectory(ConfigDirectory);
                using (FileStream fs = File.Create(ConfigPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(fs, settings);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save settings to " + ConfigPath, ex);
            }
        }
    }
}
