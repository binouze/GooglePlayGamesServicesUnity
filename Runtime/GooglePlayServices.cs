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
            GPGSLogger.SetEnabled( enabled );
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
            GPGSLogger.Log( $"[GPGS] {str}" );
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
                EditorHelper.ShowInputDialog( "connect gpgs ?<br>enter a fake Google userID and GPGS userID", "yes", "no",
                    ( uid, gpgsId ) =>
                    {
                        OnSignInResponse         = OnComplete;
                        IsSilentSignIn           = false;
                        GPGSManager.FAKE_UID     = uid;
                        GPGSManager.FAKE_GPGS_ID = gpgsId;
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
                Log( $"GPGSId:      {User.GPGSId}" );
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

            if( IsConnected && User == null )
            {
                Log( "RESULT WITHOUT USER:: NOT CONNECTED" );
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
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                             A C H I E V E M E N T S                                                                     ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Unlock success.
        /// </summary>
        [UsedImplicitly]
        public static void UnlockAchievement(string achievementId)
        {
            if( !IsConnected )
            {
                Log( "User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().UnlockAchievement(achievementId);
        }
        
        /// <summary>
        /// Increment success progress.
        /// </summary>
        [UsedImplicitly]
        public static void IncrementAchievement( string achievementId, int increment )
        {
            if( !IsConnected )
            {
                Log( "User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().IncrementAchievement(achievementId, increment);
        }
        
        /// <summary>
        /// set success progress.
        /// </summary>
        [UsedImplicitly]
        public static void SetStepsAchievement( string achievementId, int steps )
        {
            if( !IsConnected )
            {
                Log( "User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().SetStepsAchievement(achievementId, steps);
        }

        /// <summary>
        /// Show GPGS Achievement UI
        /// </summary>
        [UsedImplicitly]
        public static void ShowAchievementsUI()
        {
            if( !IsConnected )
            {
                Log( "User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().ShowAchievementsUI();
        }


        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                               C L O U D   S A V E                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------


        /// <summary>
        /// Save a game state to the cloud
        /// </summary>
        /// <param name="saveName">the save name to save</param>
        /// <param name="strData">the data to send to the cloud</param>
        /// <param name="callback">the callback to know when operation complete with the success info</param>
        [UsedImplicitly]
        public static void SaveToCloud( string saveName, string strData, Action<bool> callback )
        {
            if( !IsConnected )
            {
                Log( "User is not connected" );
                callback?.Invoke( false );
                return;
            }
            
            GPGSManager.GetInstance().SaveToCloud(saveName, strData, callback);
        }

        /// <summary>
        /// Load a game state from the cloud
        /// </summary>
        /// <param name="saveName">the save name to retrieve</param>
        /// <param name="callback">an action to get the result (bool succes, string strDatas)</param>
        [UsedImplicitly]
        public static  void LoadFromCloud( string saveName, Action<bool, string> callback )
        {
            if( !IsConnected )
            {
                Log( "User is not connected" );
                callback?.Invoke( false, "not connected" );
                return;
            }
            
            GPGSManager.GetInstance().LoadFromCloud(saveName, callback);
        }
    }
}
#endif