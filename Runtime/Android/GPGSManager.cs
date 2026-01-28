#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using com.binouze.gpgs.Helpers;
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
        
        public static Action<GPGSUser> OnAuthenticationFinished;

        
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
            cls.CallStatic("configure", configuration.WebClientId, configuration.RequestAuthCode, configuration.RequestEmail, configuration.RequestProfile );
            #endif
        }
        
        
        public void CloseDialog()
        {
            Log( "Calling CloseDialog" );
            
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("closeDialog");
            #endif
        }
        
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
        
        public void Disconnect()
        {
            Log( "Calling Disconnect" );
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("disconnect");
            #else
            OnGPGSSignInResult( "{\"deco\":\"ok\"}" );
            #endif
        }

        public void UnlockAchievement( string achievementId )
        {
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("unlockAchievement", achievementId);
            #endif
        }
        
        public void ShowAchievementsUI()
        {
            #if !UNITY_EDITOR
            using var cls = new AndroidJavaClass(JavaClassName);
            cls.CallStatic("showAchievements");
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
                OnAuthenticationFinished( signedInUser );
            }
            else
            {
                OnAuthenticationFinished( new GPGSUser{Status = GPGSSignInStatusCode.Error} );
            }
        }


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