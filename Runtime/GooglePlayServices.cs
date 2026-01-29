#if UNITY_ANDROID
using System;
using com.binouze.gpgs.Android;
using com.binouze.gpgs.Helpers;
using JetBrains.Annotations;
using UnityEngine;

namespace com.binouze.gpgs
{
    public static class GooglePlayServices
    {
        private static Action OnSignInResponse;

        [UsedImplicitly]
        public static bool     IsConnected {get; private set;}
        [UsedImplicitly]
        public static GPGSUser User        {get; private set;}
        
        static GooglePlayServices()
        {
            var settings = GPGSSettings.LoadInstance();
            if( settings == null )
            {
                Debug.LogException( new Exception("[GooglePlayServices] Fail to load settings file") );
                return;
            }
            
            var configuration = new GPGSConfiguration
            {
                WebClientId      = settings.OAuthClientID,
                AutoSignIn       = settings.AutoSignIn,
                RequestProfile   = true,
                RequestEmail     = true,
                RequestAuthCode  = true
            };
            GPGSManager.GetInstance().SetConfiguration( configuration );
            GPGSManager.OnAuthenticationFinished = OnAuthenticationFinished;
            ResetStatics();
        }

        /// <summary>
        /// Set the configuration
        /// </summary>
        [UsedImplicitly]
        public static void SetConfiguration( GPGSConfiguration configuration )
        {
            if( configuration.WebClientId == null )
            {
                var settings = GPGSSettings.LoadInstance();
                if( settings == null )
                {
                    Debug.LogException( new Exception("[GooglePlayServices] Fail to load settings file") );
                    return;
                }
                configuration.WebClientId = settings.OAuthClientID;
                configuration.AutoSignIn  = settings.AutoSignIn;
            }
            
            GPGSManager.GetInstance().SetConfiguration( configuration );
        }
        
        /// <summary>
        /// Enable or disable logs fromm the plugin
        /// </summary>
        [UsedImplicitly]
        public static void SetLoggingEnabled( bool enabled )
        {
            GPGSManager.SetLoggingEnabled( enabled );
        }
        
        /// <summary>
        /// Resets all static variables and close opened dialogs if possible
        /// </summary>
        [UsedImplicitly]
        public static void ResetStatics()
        {
            Log( "ResetStatics" );
            
            User               = null;
            IsConnected        = false;
            IsSilentSignIn     = false;
            IsSilentSignInOnly = true;
            OnSignInResponse   = null;
            
            GPGSManager.GetInstance().ResetStatics();
        }
        private static void Log( string str )
        {
            GPGSLogger.Log( $"[Google] {str}" );
        }

        private static bool IsSilentSignIn;
        private static bool IsSilentSignInOnly;
        
        /// <summary>
        /// Start GPGS Sign In process
        /// </summary>
        [UsedImplicitly]
        public static void SignIn( Action OnComplete, bool silent = false, bool silentOnly = false )
        {
            if( IsConnected && User != null )
            {
                // deja connecte
                OnComplete?.Invoke();
            }
            else
            {
                Log( "Calling SignIn" );

                #if UNITY_EDITOR
                EditorHelper.ShowInputDialog( "connect gpgs ?<br>enter a fake GPGS user id", "yes", "no",
                    uid =>
                    {
                        OnSignInResponse      = OnComplete;
                        IsSilentSignIn        = false;
                        GPGSManager.FAKE_UID = uid;
                        GPGSManager.GetInstance().SignIn();
                    },
                    () =>
                    {
                        OnComplete?.Invoke();
                    } );
                #else
                OnSignInResponse = OnComplete;
                
                if( silent )
                {
                    IsSilentSignIn     = true;
                    IsSilentSignInOnly = silentOnly;
                    GPGSManager.GetInstance().SignInSilently();
                }
                else
                {
                    IsSilentSignIn = false;
                    GPGSManager.GetInstance().SignIn();
                }
                #endif
            }
        }

        /// <summary>
        /// Sign Out (it only sign out in the script as this is not possible to sign out from google play services)
        /// </summary>
        [UsedImplicitly]
        public static void SignOut( Action OnComplete )
        {
            Log( "SignOut" );
            
            IsConnected      = false;
            User             = null;
            OnSignInResponse = OnComplete;
            GPGSManager.GetInstance().SignOut();
        }
        
        private static void OnAuthenticationFinished( GPGSUser user )
        {
            Log( $"OnAuthenticationFinished Status: {user?.Status}" );
            
            if( user is { Status: GPGSSignInStatusCode.Success or GPGSSignInStatusCode.SuccessCached } )
            {
                User = user;

                Log( $"IDToken:     {User.IdToken}" );
                Log( $"UserID:      {User.UserId}" );
                Log( $"DisplayName: {User.DisplayName}" );
                Log( $"GivenName:   {User.GivenName}" );
                Log( $"FamilyName:  {User.FamilyName}" );
                Log( $"PhotoUrl:    {User.PhotoUrl}" );
                Log( $"Email:       {User.Email}" );
                Log( $"AuthCode:    {User.AuthCode}" );
                Log( $"STATUS:      {User.Status}" );

                IsConnected = true;
            }

            // s'assurer que le user soit null si on est pas connecté
            if( !IsConnected )
                User = null;

            if( User == null )
            {
                Log( "[ConnectWithGoogle] RESULT WITHOUT USER:: NOT CONNECTED" );
                IsConnected = false;
            }

            // si c'etait un login silencieux echoué, on tente un login normal
            if( !IsConnected && IsSilentSignIn && !IsSilentSignInOnly )
            {
                SignIn( OnSignInResponse, false );
                return;
            }
            
            
            // invoke the callback
            OnSignInResponse?.Invoke();
        }
        
        
        /// <summary>
        /// Débloque un succès.
        /// </summary>
        [UsedImplicitly]
        public static void UnlockAchievement(string achievementId)
        {
            if( !IsConnected )
                return;
            
            GPGSManager.GetInstance().UnlockAchievement(achievementId);
        }

        /// <summary>
        /// Affiche l'interface native des succès.
        /// </summary>
        [UsedImplicitly]
        public static void ShowAchievementsUI()
        {
            if( !IsConnected )
                return;
            
            GPGSManager.GetInstance().ShowAchievementsUI();
        }
    }
}
#endif