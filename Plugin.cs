using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BaboonAPI.Hooks.Initializer;
using BaboonAPI.Hooks.Tracks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TrombLoader.CustomTracks;
using TrombLoader.Helpers;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace TrombLoader
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ch.offbeatwit.baboonapi.plugin", "2.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public ShaderHelper ShaderHelper;

        public ConfigEntry<int> beatsToShow;
        public ConfigEntry<bool> DeveloperMode;
        public ConfigEntry<string> DefaultBackground;
        public ConfigEntry<bool> turboBackgroundFallback;

        private Harmony _harmony = new(PluginInfo.PLUGIN_GUID);

        private void Awake()
        {
            var customFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "TrombLoader.cfg"), true);
            beatsToShow = customFile.Bind("General", "Note Display Limit", 64, "The maximum amount of notes displayed on screen at once.");
            var backgrounds = new List<string>{"freeplay", "freeplay-static", "grey", "black"};
            DefaultBackground = customFile.Bind("General", "Default Background", "freeplay", 
                $"The default background to show when a chart does not include one. Can be one of the following:\n{string.Join(", ", backgrounds)}");
            DefaultBackground.Value = DefaultBackground.Value.ToLower().Trim();
            if (!backgrounds.Contains(DefaultBackground.Value))
            {
                LogWarning("Default Background is not set to a valid option!");
            }

            turboBackgroundFallback = customFile.Bind("General", "Turbo Mode Background Fallback", false,
                "When enabled, TrombLoader will load an image or default background instead of a video background when Turbo Mode is on.");

            DeveloperMode = customFile.Bind("Charting", "Developer Mode", false,
                "When enabled, TrombLoader will re-read chart data from disk each time a track is loaded.");

            Instance = this;
            LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            GameInitializationEvent.Register(Info, TryInitialize);
            TrackRegistrationEvent.EVENT.Register(new TrackLoader());
            TrackCollectionRegistrationEvent.EVENT.Register(new TrombLoaderCollection.CollectionLoader(this));

            ShaderHelper = new();
            QualitySettings.pixelLightCount = 16;
        }

        private void TryInitialize()
        {
            _harmony.PatchAll();
        }

        public IEnumerator GetAudioClipSync(string path, Action callback = null)
        {
            var uri = new UriBuilder(Uri.UriSchemeFile, string.Empty)
            {
                Path = path
            }.Uri;

            var www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
            yield return www.SendWebRequest();
            while (!www.isDone)
                yield return null;

            if (www.isNetworkError || www.isHttpError)
            {
                yield return www.error;
            }
            else
            {
                callback?.Invoke();
                yield return DownloadHandlerAudioClip.GetContent(www);
            }
        }

        public void LoadGameplayScene()
        {
            SceneManager.LoadSceneAsync("gameplay", LoadSceneMode.Single);
        }

        #region logging
        internal static void LogDebug(string message) => Instance.Log(message, LogLevel.Debug);
        internal static void LogInfo(string message) => Instance.Log(message, LogLevel.Info);
        internal static void LogWarning(string message) => Instance.Log(message, LogLevel.Warning);
        internal static void LogError(string message) => Instance.Log(message, LogLevel.Error);
        private void Log(string message, LogLevel logLevel) => Logger.Log(logLevel, message);
        #endregion
    }
}
