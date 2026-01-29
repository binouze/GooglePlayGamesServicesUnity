#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using com.binouze.gpgs.Helpers;
using UnityEditor;
using UnityEngine;

namespace com.binouze.gpgs.Android
{
    public class GPGSManager : MonoBehaviour
    {
        #if UNITY_EDITOR
        private static string SUCCESS_RESPONSE => "{\"result\":{\"Status\":0,\"Email\":\"fakeuser@gmail.com\",\"FamilyName\":\"FAKE\",\"GPGSId\":\""+FAKE_GPGS_ID+"\",\"UserId\":\""+FAKE_UID+"\",\"DisplayName\":\"User FAKE\",\"GivenName\":\"User\",\"PhotoUrl\":\"\"}}";
        #else
        public const string JavaClassName = "com.binouze.GPGSHelper";
        #endif
        
        public static string FAKE_UID;
        public static string FAKE_GPGS_ID;
        
        internal static Action<GPGSUser> OnAuthenticationFinished;
        private Action<bool>             OnDataSaved;
        private Action<bool,string>      OnDataRead;

        
        private static void Log( string val )
        {
            GPGSLogger.Log( $"[GPGSManager] {val}" );
        }
        private static void LogError( string val )
        {
            GPGSLogger.Log( $"[GPGSManager] {val}" );
        }
        
        public static void SetLoggingEnabled( bool enabled )
        {
            #if !UNITY_EDITOR
            try
            {
                
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("enableDebugLogging", enabled);
            }
            catch( Exception e )
            {
                LogError( $"SetLoggingEnabled {e}" );
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
                LogError( $"SetConfiguration {e}" );
            }
            #endif
        }
        
        
        public void ResetStatics()
        {
            Log( "Calling ResetStatics" );

            OnDataRead  = null;
            OnDataSaved = null;

            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("closeDialog");
            }
            catch( Exception e )
            {
                LogError( $"ResetStatics {e}" );
            }
            #endif
        }
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                         S I G N   I N   /   S I G N   O U T                                                             ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        public void SignIn()
        {
            Log( "Calling SignIn" );
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("signIn");
            }
            catch( Exception e )
            {
                LogError( $"SignIn {e}" );
            }
            #else
            OnGPGSSignInResult( SUCCESS_RESPONSE );
            #endif
        }

        public void SignInSilently()
        {
            Log( "Calling SignInSilently" );
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("signInSilently");
            }
            catch( Exception e )
            {
                LogError( $"SignInSilently {e}" );
            }
            #else
            OnGPGSSignInResult( SUCCESS_RESPONSE );
            #endif
        }
        
        public void SignOut()
        {
            Log( "Calling SignOut" );
            
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("signOut");
            }
            catch( Exception e )
            {
                LogError( $"SignOut {e}" );
            }
            #else
            OnGPGSSignInResult( "{\"deco\":\"ok\"}" );
            #endif
        }
        
        /// <summary>
        /// La methode appelee par le plugin natif pour renvoyer les resultats de login
        /// </summary>
        /// <param name="result"></param>
        public void OnGPGSSignInResult( string result )
        {
            Log( $"OnSignInResult Result: {result}" );
            
            var datas = GPGSJson.Deserialize( result );
            if( datas is Dictionary<string, object> dic )
            {
                var signedInUser = GPGSUser.FromObject( dic.GetDictionary( "result" ) );
                OnAuthenticationFinished?.Invoke( signedInUser );
            }
            else
            {
                OnAuthenticationFinished?.Invoke( new GPGSUser{Status = GPGSSignInStatusCode.Error} );
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                             A C H I E V E M E N T S                                                                     ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        
        public void UnlockAchievement( string achievementId )
        {
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("unlockAchievement", achievementId);
            }
            catch( Exception e )
            {
                LogError( $"UnlockAchievement {e}" );
            }
            #endif
        }
        
        public void IncrementAchievement( string achievementId, int increment )
        {
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("incrementAchievement", achievementId, increment);
            }
            catch( Exception e )
            {
                LogError( $"IncrementAchievement {e}" );
            }
            #endif
        }
        
        public void SetStepsAchievement( string achievementId, int steps )
        {
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("setStepAchievement", achievementId, steps);
            }
            catch( Exception e )
            {
                LogError( $"SetStepsAchievement {e}" );
            }
            #endif
        }
        
        public void ShowAchievementsUI()
        {
            #if !UNITY_EDITOR
            try
            {
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("showAchievements");
            }
            catch( Exception e )
            {
                LogError( $"ShowAchievementsUI {e}" );
            }
            #endif
        }
        
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                               C L O U D   S A V E                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        // -- WRITE --
        
        internal void SaveToCloud( string saveName, string strData, Action<bool> callback )
        {
            #if !UNITY_EDITOR
            try
            {
                OnDataSaved   = callback;
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("setCloudSaveDatas",saveName,strData);
            }
            catch( Exception e )
            {
                LogError( $"SaveToCloud {e}" );
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
            Log( $"OnGPGSCloudSaveWriteResult Result: {data}" );
            var status = OnDataSaved.GetInt(-10);
            OnDataSaved?.Invoke( status == 0 );
            OnDataSaved = null;
        }
        
        
        // -- READ --

        internal void LoadFromCloud( string saveName, Action<bool,string> callback )
        {
            #if !UNITY_EDITOR
            try
            {
                OnDataRead = callback;
                using var cls = new AndroidJavaClass(JavaClassName);
                cls.CallStatic("getCloudSaveDatas");
            }
            catch( Exception e )
            {
                LogError( $"LoadFromCloud {e}" );
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
            
            // si on a un entier negatif c'est que c'est une erreur
            if( int.TryParse(data, out var errorCode) && errorCode < 0 )
            {
                OnDataRead?.Invoke( false, data );
            }
            else
            {
                OnDataRead?.Invoke( true, data );
            }
            
            OnDataRead = null;
        }
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                                 S I N G L E T O N                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------

        private static GPGSManager _instance;
        public static GPGSManager GetInstance()
        {
            if( _instance == null )
            {
                _instance = FindAnyObjectByType<GPGSManager>();
                if( _instance == null )
                {
                    const string goName = "GPGSManagerObject";
                    var          go     = GameObject.Find( goName );
                    if( go == null )
                    {
                        go = new GameObject { name = goName };
                        DontDestroyOnLoad( go );
                    }
                    _instance = go.AddComponent<GPGSManager>();
                }
            }

            return _instance;
        }
    }
}
#endif