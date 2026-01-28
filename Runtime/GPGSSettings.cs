#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

using UnityEngine;

namespace com.binouze.gpgs
{
    public class GPGSSettings : ScriptableObject
    {
        public const string GPGSSettingsFile          = "GPGSSettings";
        public const string GPGSSettingsResDir        = "Assets/LagoonPlugins/GooglePlayServices/Resources";
        public const string GPGSSettingsFileExtension = ".asset";

        
        #if UNITY_EDITOR
        public static GPGSSettings LoadSettingsInstance()
        {
            var instance = LoadInstance();
            // Create instance if null.
            if( instance == null )
            {
                Directory.CreateDirectory(GPGSSettingsResDir);
                instance = CreateInstance<GPGSSettings>();
                var assetPath = Path.Combine( GPGSSettingsResDir, GPGSSettingsFile + GPGSSettingsFileExtension);
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
            }
            return instance;
        }
        [MenuItem("LagoonPlugins/SignInWithAppleOrGoogle Settings")]
        public static void OpenInspector()
        {
            Selection.activeObject = LoadSettingsInstance();
        }
        #endif
        
        
        public static GPGSSettings LoadInstance()
        {
            // Read from resources.
            return Resources.Load<GPGSSettings>(GPGSSettingsFile);
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty( _WebClientID ) && !string.IsNullOrEmpty( _GPGS_ID );
        }
        
        // -- GPGS
        
        [SerializeField][Tooltip("enable/disable auto sign in when the app launch")]
        private bool _AutoSignIn;
        [SerializeField][Tooltip("Game's GPGS ID found in the Google Play Console")]
        private string _GPGS_ID;
        [SerializeField][TextArea][Tooltip("Game's Web Client ID found in the Google Play Console")]
        private string _WebClientID = string.Empty;
        
        public string GPGS_ID
        {
            get => _GPGS_ID;
            private set => _GPGS_ID = value;
        }
        public string WebClientID
        {
            get => _WebClientID;
            private set => _WebClientID = value;
        }
        public bool AutoSignIn
        {
            get => _AutoSignIn;
            private set => _AutoSignIn = value;
        }
    }
}