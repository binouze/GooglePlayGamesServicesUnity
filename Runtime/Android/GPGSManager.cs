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
        private static string SUCCESS_RESPONSE => "{\"result\":{\"Status\":0,\"Email\":\"fakeuser@gmail.com\",\"FamilyName\":\"FAKE\",\"UserId\":\""+FAKE_UID+"\",\"DisplayName\":\"User FAKE\",\"GivenName\":\"User\",\"PhotoUrl\":\"\"}}";
        #else
        public const string JavaClassName = "com.binouze.GPGSHelper";
        #endif
        
        public static string FAKE_UID;
        
        internal static Action<GPGSUser> OnAuthenticationFinished;
        private Action<bool>             OnDataSaved;
        private Action<bool,string>      OnDataRead;

        
        private static void Log( string val )
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
                
            }
            #endif
        }
        
        public void SetConfiguration( GPGSConfiguration configuration )
        {
            Log( "Calling SetConfiguration" );
            
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("configure", configuration.WebClientId, configuration.RequestAuthCode, configuration.RequestEmail, configuration.RequestProfile, configuration.AutoSignIn );
            #endif
        }
        
        
        public void ResetStatics()
        {
            Log( "Calling ResetStatics" );

            OnDataRead  = null;
            OnDataSaved = null;

            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("closeDialog");
            #endif
        }
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                         S I G N   I N   /   S I G N   O U T                                                             ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        public void SignIn()
        {
            Log( "Calling SignIn" );
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("signIn");
            #else
            OnGPGSSignInResult( SUCCESS_RESPONSE );
            #endif
        }

        public void SignInSilently()
        {
            Log( "Calling SignInSilently" );
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("signInSilently");
            #else
            OnGPGSSignInResult( SUCCESS_RESPONSE );
            #endif
        }
        
        public void SignOut()
        {
            Log( "Calling SignOut" );
            
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("signOut");
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
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("unlockAchievement", achievementId);
            #endif
        }
        
        public void IncrementAchievement( string achievementId, int increment )
        {
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("incrementAchievement", achievementId, increment);
            #endif
        }
        
        public void SetStepsAchievement( string achievementId, int steps )
        {
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("setStepAchievement", achievementId, steps);
            #endif
        }
        
        public void ShowAchievementsUI()
        {
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("showAchievements");
            #endif
        }
        
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                               C L O U D   S A V E                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        // -- WRITE --
        
        public void SaveToCloud( string saveName, string strData, Action<bool> callback )
        {
            #if !UNITY_EDITOR
            OnDataSaved   = callback;
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("setCloudSaveDatas",saveName,strData);
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
        }
        
        
        // -- READ --

        public void LoadFromCloud( string saveName, Action<bool,string> callback )
        {
            #if !UNITY_EDITOR
            OnDataRead = callback;
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("getCloudSaveDatas");
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
                return; 
            }
            
            OnDataRead?.Invoke( true, data );
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