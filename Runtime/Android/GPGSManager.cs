#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using com.binouze.gpgs.Helpers;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Security.Cryptography;

namespace com.binouze.gpgs.Android
{
    public class GPGSManager : MonoBehaviour
    {
        #if UNITY_EDITOR
        private static string SUCCESS_RESPONSE => "{\"result\":{\"Status\":0,\"Email\":\"fakeuser@gmail.com\",\"FamilyName\":\"FAKE\",\"GPGSId\":\""+FAKE_GPGS_ID+"\",\"UserId\":\""+FAKE_UID+"\",\"DisplayName\":\"User FAKE\",\"GivenName\":\"User\",\"PhotoUrl\":\"\"}}";
        private const  string SUCCESS_SIGN_OUT =  "{\"result\":{\"Status\":-2}}";
        #else
        public const string JavaClassName = "com.binouze.GPGSHelper";
        #endif
        
        public static string FAKE_UID;
        public static string FAKE_GPGS_ID;
        
        private Action<bool>        OnDataSaved;
        private Action<bool,string> OnDataRead;
        private Action<GPGSUser>    OnAuthCallback;

        
        private static void Log( string val )
        {
            GPGSLogger.Log( $"[GPGSManager] {val}" );
        }
        private static void LogError( string val )
        {
            GPGSLogger.LogError( $"[GPGSManager] ERROR: {val}" );
        }
        
        public static void SetLoggingEnabled( bool enabled )
        {
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("enableDebugLogging", enabled);

                if( enabled )
                {
                    var sha256 = GetAndroidHash("SHA256");
                    var sha1   = GetAndroidHash("SHA1");
                    Log("Android SHA-256:" + sha256);
                    Log("Android SHA-1:  " + sha1);
                }
            }
            catch( Exception e )
            {
                LogError( $"SetLoggingEnabled - {e}" );
            }
            #endif
        }
        
        public void SetConfiguration( GPGSConfiguration configuration )
        {
            Log( "Calling SetConfiguration" );
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("configure", configuration.WebClientId, configuration.RequestAuthCode, configuration.RequestEmail, configuration.RequestProfile, configuration.AutoSignIn );
            }
            catch( Exception e )
            {
                LogError( $"SetConfiguration - {e}" );
            }
            #endif
        }
        
        
        public void ResetStatics()
        {
            Log( "Calling ResetStatics" );

            OnDataRead     = null;
            OnDataSaved    = null;
            OnAuthCallback = null;

            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("closeDialog");
            }
            catch( Exception e )
            {
                LogError( $"ResetStatics - {e}" );
            }
            #endif
        }
        
        
        private bool? _isGpgsSupported = null;
        public bool IsSupported()
        {
            #if UNITY_EDITOR
            return true; 
            #else
            if( _isGpgsSupported.HasValue ) 
                return _isGpgsSupported.Value;

            try
            {
                using var cls    = new AndroidJavaClass(JavaClassName);
                _isGpgsSupported = cls.CallStatic<bool>("isServicesAvailable");
            }
            catch( Exception e )
            {
                LogError($"IsSupported check failed: {e}");
                _isGpgsSupported = false;
            }
            return _isGpgsSupported.Value;
            #endif
        }
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                         S I G N   I N   /   S I G N   O U T                                                             ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        public void SignIn(Action<GPGSUser> callback)
        {
            Log( "Calling SignIn" );
            
            OnAuthCallback = callback;
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("signIn");
            }
            catch( Exception e )
            {
                LogError( $"SignIn - {e}" );
                OnAuthCallback = null;
                callback?.Invoke(null);
            }
            #else
            OnGPGSSignInResult( SUCCESS_RESPONSE );
            #endif
        }

        public void SignInSilently(Action<GPGSUser> callback)
        {
            Log( "Calling SignInSilently" );
            
            OnAuthCallback = callback;
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("signInSilently");
            }
            catch( Exception e )
            {
                LogError( $"SignInSilently - {e}" );
                OnAuthCallback = null;
                callback?.Invoke(null);
            }
            #else
            OnGPGSSignInResult( SUCCESS_RESPONSE );
            #endif
        }
        
        public void SignOut(Action<GPGSUser> callback)
        {
            Log( "Calling SignOut" );
            
            OnAuthCallback = callback;
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("signOut");
            }
            catch( Exception e )
            {
                LogError( $"SignOut - {e}" );
                OnAuthCallback = null;
                callback?.Invoke(null);
            }
            #else
            OnGPGSSignInResult( SUCCESS_SIGN_OUT);
            #endif
        }
        
        /// <summary>
        /// La methode appelee par le plugin natif pour renvoyer les resultats de login
        /// </summary>
        /// <param name="result"></param>
        public void OnGPGSSignInResult( string result )
        {
            Log( $"OnSignInResult Result: {result}" );
            
            var data = GPGSJson.Deserialize( result );
            var signedInUser = (data is Dictionary<string, object> dic) 
                ? GPGSUser.FromObject(dic.GetDictionary("result")) 
                : new GPGSUser { Status = GPGSSignInStatusCode.Error };

            var cb = OnAuthCallback;
            OnAuthCallback = null;
            cb?.Invoke(signedInUser);
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                             A C H I E V E M E N T S                                                                     ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        
        public void UnlockAchievement( string achievementId )
        {
            Log( $"Calling UnlockAchievement {achievementId}" );
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("unlockAchievement", achievementId);
            }
            catch( Exception e )
            {
                LogError( $"UnlockAchievement - {e}" );
            }
            #endif
        }
        
        public void IncrementAchievement( string achievementId, int increment )
        {
            Log( $"Calling IncrementAchievement {achievementId} {increment}" );
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("incrementAchievement", achievementId, increment);
            }
            catch( Exception e )
            {
                LogError( $"IncrementAchievement - {e}" );
            }
            #endif
        }
        
        public void SetStepsAchievement( string achievementId, int steps )
        {
            Log( $"Calling SetStepsAchievement {achievementId} {steps}" );
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("setStepAchievement", achievementId, steps);
            }
            catch( Exception e )
            {
                LogError( $"SetStepsAchievement - {e}" );
            }
            #endif
        }
        
        public void ShowAchievementsUI()
        {
            Log( "Calling ShowAchievementsUI" );
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("showAchievements");
            }
            catch( Exception e )
            {
                LogError( $"ShowAchievementsUI - {e}" );
            }
            #endif
        }
        
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                               C L O U D   S A V E                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        // -- WRITE --
        
        internal void SaveToCloud( string saveName, string strData, Action<bool> callback )
        {
            Log( $"Calling SaveToCloud {saveName} {strData}" );
            
            #if !UNITY_EDITOR
            try
            {
                OnDataSaved   = callback;
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("setCloudSaveDatas",saveName,strData);
            }
            catch( Exception e )
            {
                LogError( $"SaveToCloud - {e}" );
                OnDataSaved = null;
                callback?.Invoke( false );
            }
            #else
            EditorPrefs.SetString( $"GPGS_DATA_{saveName}", strData );
            callback?.Invoke( true );
            #endif
        }

        public void OnGPGSCloudSaveWriteResult( string data )
        {
            var parseSuccess = int.TryParse(data, out var statusCode);
            var status       = parseSuccess && statusCode == 0;

            Log($"OnGPGSCloudSaveWriteResult Result: {data} - Status: {statusCode} (Parsed: {parseSuccess})");

            var cb = OnDataSaved;
            OnDataSaved = null;
            cb?.Invoke( status );
        }
        
        
        // -- READ --

        internal void LoadFromCloud( string saveName, Action<bool,string> callback )
        {
            Log( $"Calling LoadFromCloud {saveName}" );
            
            #if !UNITY_EDITOR
            try
            {
                OnDataRead = callback;
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("getCloudSaveDatas", saveName);
            }
            catch( Exception e )
            {
                LogError( $"LoadFromCloud - {e}" );
                OnDataRead = null;
                callback?.Invoke( false, "" );
            }
            #else
            var data = EditorPrefs.GetString( $"GPGS_DATA_{saveName}" );
            callback?.Invoke( true, data );
            #endif
        }
        
        public void OnGPGSCloudSaveReadResult( string data )
        {
            Log( $"OnGPGSCloudSaveReadResult Result: {data}" );
            
            var cb = OnDataRead;
            OnDataRead = null;
            
            // si on a un entier negatif c'est que c'est une erreur
            if( int.TryParse(data, out var errorCode) && errorCode < 0 )
            {
                cb?.Invoke( false, data );
            }
            else
            {
                cb?.Invoke( true, data );
            }
        }
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                                 S I N G L E T O N                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------

        private static GPGSManager _instance;
        public static GPGSManager GetInstance()
        {
            if( !_instance )
            {
                _instance = FindAnyObjectByType<GPGSManager>();
                if( !_instance )
                {
                    const string goName = "GPGSManagerObject";
                    var          go     = GameObject.Find( goName );
                    if( !go )
                    {
                        go = new GameObject { name = goName };
                        DontDestroyOnLoad( go );
                    }
                    _instance = go.AddComponent<GPGSManager>();
                }
            }

            return _instance;
        }
        
        // DEBUG
        
        public static string GetAndroidHash(string algorithm)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using( AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer") )
                {
                    using( AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity") )
                    {
                        string packageName = currentActivity.Call<string>("getPackageName");
                        AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
                        
                        // 64 = GET_SIGNATURES
                        AndroidJavaObject packageInfo  = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 64);
                        AndroidJavaObject[] signatures = packageInfo.Get<AndroidJavaObject[]>("signatures");

                        if (signatures != null && signatures.Length > 0)
                        {
                            byte[] certBytes = signatures[0].Call<byte[]>("toByteArray");
                            byte[] hashBytes;

                            if( algorithm == "SHA256" ) 
                            {
                                using var sha256 = SHA256.Create();
                                hashBytes = sha256.ComputeHash(certBytes);
                            } 
                            else 
                            {
                                using var sha1 = SHA1.Create();
                                hashBytes = sha1.ComputeHash(certBytes);
                            }

                            return BitConverter.ToString(hashBytes).Replace("-", ":");
                        }
                    }
                }
            }
            catch( Exception e )
            {
                LogError("Erreur signature: " + e.Message);
            }
            #else
            LogError("Le hash n'est récupérable que sur un vrai appareil Android.");
            #endif
            return "N/A";
        }
    }
}
#endif