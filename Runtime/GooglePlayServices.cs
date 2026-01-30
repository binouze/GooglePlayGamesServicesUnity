#if UNITY_ANDROID
using System;
using System.Threading;
using com.binouze.gpgs.Android;
using com.binouze.gpgs.Helpers;
using JetBrains.Annotations;
using UnityEngine;

namespace com.binouze.gpgs
{
    /// <summary>
    /// The primary entry point for the Google Play Games Services plugin. 
    /// Provides access to Authentication, Achievements, and Cloud Save features.
    /// </summary>
    public static class GooglePlayServices
    {
        private static Action<GPGSUser> OnSignInResponse;

        private static CancellationTokenSource _globalCts = new ();
        private static CancellationToken       GlobalToken => _globalCts.Token;
        
        private static readonly SemaphoreSlim _semaphoreSignIn = new ( 1, 1 );
        
        /// <summary>
        /// Gets a value indicating whether a user is currently authenticated with Google Play Games.
        /// </summary>
        [UsedImplicitly] public static bool                 IsConnected      {get; private set;}
        /// <summary>
        /// Gets the currently authenticated user information. 
        /// Returns null if <see cref="IsConnected"/> is false.
        /// </summary>
        [UsedImplicitly] public static GPGSUser             User             {get; private set;}
        /// <summary>
        /// Gets the status code of the last attempted Sign-In operation.
        /// </summary>
        [UsedImplicitly] public static GPGSSignInStatusCode LastSignInStatus {get; private set;}
        
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
        /// Updates the current plugin configuration. 
        /// Use this if you need to override settings:
        /// - RequestAuthCode
        /// - RequestEmail
        /// - RequestProfile
        /// </summary>
        /// <param name="configuration">The configuration object containing client IDs and requested scopes.</param>
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
        /// Enables or disables internal debug logging for both C# and Java layers.
        /// </summary>
        /// <param name="enabled">True to show logs in the console/Logcat, false to hide them.</param>
        [UsedImplicitly]
        public static void SetLoggingEnabled( bool enabled )
        {
            GPGSLogger.SetEnabled( enabled );
            GPGSManager.SetLoggingEnabled( enabled );
        }
        
        /// <summary>
        /// Resets all local user data and closes any active native Google Play Games dialogs.
        /// </summary>
        [UsedImplicitly]
        public static void ResetStatics()
        {
            Log( "ResetStatics" );
            
            _globalCts.Cancel();
            _globalCts.Dispose();
            _globalCts = new CancellationTokenSource();
            
            User             = null;
            IsConnected      = false;
            OnSignInResponse = null;
            LastSignInStatus = GPGSSignInStatusCode.SignoutSuccess;
            
            GPGSManager.GetInstance().ResetStatics();
        }
        private static void Log( string str )
        {
            GPGSLogger.Log( $"[GPGS] {str}" );
        }
        private static void LogError( string str )
        {
            GPGSLogger.LogError( $"[GPGS] ERROR: {str}" );
        }


        /// <summary>
        /// Initiates the Google Play Games authentication flow.
        /// </summary>
        /// <param name="OnComplete">Callback executed when the authentication process finishes (success or failure).</param>
        /// <param name="silent">If true, attempts to sign in without showing any UI to the user.</param>
        /// <param name="silentOnly">If true, the process stops if silent sign-in fails, without prompting for interactive sign-in.</param>
        /// <param name="upgradeSilentSignInIfDeveloperError">If true and a Developer Error occurs during silent sign-in, it will retry with the interactive UI.</param>
        [UsedImplicitly]
        public static void SignIn( Action OnComplete, bool silent = false, bool silentOnly = false, bool upgradeSilentSignInIfDeveloperError = false )
        {
            SignInAsync(silent, silentOnly, upgradeSilentSignInIfDeveloperError).Run(OnComplete);
        }

        /// <summary>
        /// Initiates the Google Play Games authentication flow.
        /// </summary>
        /// <param name="silent">If true, attempts to sign in without showing any UI to the user.</param>
        /// <param name="silentOnly">If true, the process stops if silent sign-in fails, without prompting for interactive sign-in.</param>
        /// <param name="upgradeSilentSignInIfDeveloperError">If true and a Developer Error occurs during silent sign-in, it will retry with the interactive UI.</param>
        /// <param name="userToken"></param>
        [UsedImplicitly]
        public static async Awaitable SignInAsync( bool silent = false, bool silentOnly = false, bool upgradeSilentSignInIfDeveloperError = false, CancellationToken userToken = default )
        {
            using var linkedCts     = CancellationTokenSource.CreateLinkedTokenSource(userToken, GlobalToken);
            var       combinedToken = linkedCts.Token;
            
            if( _semaphoreSignIn.CurrentCount == 0 )
                Log( "Waiting for previous SignIn operation to complete ..." );
            
            await _semaphoreSignIn.WaitAsync(combinedToken);
            
            var       completionSource = new AwaitableCompletionSource();
            using var _                = completionSource.LinkToken(combinedToken);
            
            try
            {
                if( IsConnected && User != null )
                    return;
                
                Log( "Calling SignIn" );

                
                // sign in
                var result = await ExecuteSignInFlow(silent,combinedToken);

                // check if we should upgrade the silent signIn to a manual one
                var isSuccess     = result is { Status: GPGSSignInStatusCode.Success or GPGSSignInStatusCode.SuccessCached };
                var shouldUpgrade = !isSuccess && silent && (!silentOnly || (upgradeSilentSignInIfDeveloperError && result?.Status == GPGSSignInStatusCode.DeveloperError));

                if( shouldUpgrade )
                {
                    Log("Silent login failed, upgrading to interactive sign-in...");
                    result = await ExecuteSignInFlow(false,combinedToken);
                }
                
                // update sign in status
                UpdateSignInState( result );
            }
            finally
            {
                OnSignInResponse = null; // cleaning callback
                _semaphoreSignIn.Release();
            }
        }
        
        // Méthode helper interne pour éviter la répétition du code de CompletionSource
        private static async Awaitable<GPGSUser> ExecuteSignInFlow(bool silent, CancellationToken token)
        {
            Log( $"ExecuteSignInFlow {silent}" );
            
            var       completionSource = new AwaitableCompletionSource<GPGSUser>();
            using var _                = completionSource.LinkToken(token);
    
            OnSignInResponse = user => completionSource.SetResult(user);

            #if UNITY_EDITOR
            // show the editor UI
            EditorHelper.ShowInputDialog( "connect gpgs ?<br>enter a fake Google userID and GPGS userID", "yes", "no",
                ( uid, gpgsId ) =>
                {
                    GPGSManager.FAKE_UID     = uid;
                    GPGSManager.FAKE_GPGS_ID = gpgsId;
                    GPGSManager.GetInstance().SignIn();
                },
                () => completionSource.SetResult(null) );
            #else
            if( silent )
            {
                GPGSManager.GetInstance().SignInSilently();
            }
            else
            {
                GPGSManager.GetInstance().SignIn();
            }
            #endif

            return await completionSource.Awaitable;
        }

        /// <summary>
        /// Signs the user out of the local session. 
        /// Note: This disconnects the app state but does not globally sign the user out of Google Play Services as it's not possible with GPGS v2.
        /// </summary>
        /// <param name="OnComplete">Callback executed when the sign-out cleanup is finished.</param>
        [UsedImplicitly]
        public static void SignOut( Action OnComplete )
        {
            SignOutAsync().Run(OnComplete);
        }
        
        /// <summary>
        /// Signs the user out of the local session. 
        /// Note: This disconnects the app state but does not globally sign the user out of Google Play Services as it's not possible with GPGS v2.
        /// </summary>
        [UsedImplicitly]
        public static async Awaitable SignOutAsync(CancellationToken userToken = default)
        {
            using var linkedCts     = CancellationTokenSource.CreateLinkedTokenSource(userToken, GlobalToken);
            var       combinedToken = linkedCts.Token;
            
            // waiting for any previous operation
            if( _semaphoreSignIn.CurrentCount == 0 )
                Log( "Waiting for previous SignIn operation to complete ..." );
            await _semaphoreSignIn.WaitAsync(combinedToken);
            
            Log( "SignOut" );

            try
            {
                // already disconnected
                if( !IsConnected )
                    return;
                
                // create the completion source
                var       completionSource = new AwaitableCompletionSource<GPGSUser>();
                using var _                = completionSource.LinkToken( combinedToken );
                
                // set the completion task
                OnSignInResponse = user => completionSource.SetResult(user);
                
                // call native signOut function
                GPGSManager.GetInstance().SignOut();
            
                // wait for the completion
                var result = await completionSource.Awaitable;
                UpdateSignInState( result );
            }
            finally
            {
                OnSignInResponse = null; // cleaning callback
                _semaphoreSignIn.Release();
            }
        }
        
        private static void OnAuthenticationFinished( GPGSUser user )
        {
            Log( $"OnAuthenticationFinished Status: {user?.Status}" );

            // keep the last sign in status code
            /*LastSignInStatus = user?.Status ?? GPGSSignInStatusCode.Error;
            
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
            else
            {
                IsConnected = false;
                User        = null;
            }*/
            
            // invoke the callback
            OnSignInResponse?.Invoke( user );
        }
        
        private static void UpdateSignInState( GPGSUser user )
        {
            LastSignInStatus = user?.Status ?? GPGSSignInStatusCode.Error;
            
            if( user is { Status: GPGSSignInStatusCode.Success or GPGSSignInStatusCode.SuccessCached } )
            {
                User        = user;
                IsConnected = true;
                Log($"UpdateSignInState - Connected as: {User.DisplayName}");
            }
            else
            {
                User        = null;
                IsConnected = false;
                Log($"UpdateSignInState - Not connected. Status: {LastSignInStatus}");
            }
        }
        
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                             A C H I E V E M E N T S                                                                     ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Unlocks the achievement with the specified ID.
        /// </summary>
        /// <param name="achievementId">The unique ID of the achievement from the Google Play Console.</param>
        [UsedImplicitly]
        public static void UnlockAchievement(string achievementId)
        {
            if( !IsConnected )
            {
                LogError( "UnlockAchievement - User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().UnlockAchievement(achievementId);
        }
        
        /// <summary>
        /// Increments an incremental achievement by the given number of steps.
        /// </summary>
        /// <param name="achievementId">The unique ID of the achievement.</param>
        /// <param name="increment">The number of steps to add to the current progress.</param>
        [UsedImplicitly]
        public static void IncrementAchievement( string achievementId, int increment )
        {
            if( !IsConnected )
            {
                LogError( "IncrementAchievement - User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().IncrementAchievement(achievementId, increment);
        }
        
        /// <summary>
        /// Directly sets the progress of an incremental achievement to a specific number of steps.
        /// </summary>
        /// <param name="achievementId">The unique ID of the achievement.</param>
        /// <param name="steps">The total number of steps to set for the current user.</param>
        [UsedImplicitly]
        public static void SetStepsAchievement( string achievementId, int steps )
        {
            if( !IsConnected )
            {
                LogError( "SetStepsAchievement - User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().SetStepsAchievement(achievementId, steps);
        }

        /// <summary>
        /// Opens the native Google Play Games overlay to display the user's achievements.
        /// </summary>
        [UsedImplicitly]
        public static void ShowAchievementsUI()
        {
            if( !IsConnected )
            {
                LogError( "ShowAchievementsUI - User is not connected" );
                return;
            }
            
            GPGSManager.GetInstance().ShowAchievementsUI();
        }


        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------
        // ---                                                               C L O U D   S A V E                                                                       ---
        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------


        /// <summary>
        /// Saves a data string to the Google Play Cloud Save service (Snapshots).
        /// </summary>
        /// <param name="saveName">The filename of the snapshot.</param>
        /// <param name="strData">The data string (usually JSON) to store.</param>
        /// <param name="callback">A callback invoked with a boolean indicating if the save was successful.</param>
        [UsedImplicitly]
        public static void SaveToCloud( string saveName, string strData, Action<bool> callback )
        {
            SaveToCloudAsync( saveName, strData ).Run( callback );
        }

        /// <summary>
        /// Saves a data string to the Google Play Cloud Save service (Snapshots).
        /// </summary>
        /// <param name="saveName">The filename of the snapshot.</param>
        /// <param name="strData">The data string (usually JSON) to store.</param>
        /// <param name="userToken"></param>
        [UsedImplicitly]
        public static async Awaitable<bool> SaveToCloudAsync(string saveName, string strData, CancellationToken userToken = default)
        {
            using var linkedCts     = CancellationTokenSource.CreateLinkedTokenSource(userToken, GlobalToken);
            var       combinedToken = linkedCts.Token;
            
            // wait for any previous action to complete
            if( _semaphoreSignIn.CurrentCount == 0 )
                Log($"Waiting for GPGS state to stabilize before saving {saveName}...");
            await _semaphoreSignIn.WaitAsync(combinedToken);

            try
            {
                // user not connected
                if( !IsConnected )
                {
                    LogError("SaveToCloud - User is not connected");
                    return false;
                }

                Log($"Calling SaveToCloud: {saveName}");
                var       completionSource = new AwaitableCompletionSource<bool>();
                using var _                = completionSource.LinkToken( combinedToken );

                GPGSManager.GetInstance().SaveToCloud(saveName, strData, success => {
                    completionSource.SetResult(success);
                });

                return await completionSource.Awaitable;
            }
            finally
            {
                _semaphoreSignIn.Release();
            }
        }

        /// <summary>
        /// Retrieves a data string from the Google Play Cloud Save service (Snapshots).
        /// </summary>
        /// <param name="saveName">The filename of the snapshot to read.</param>
        /// <param name="callback">A callback invoked with a boolean (success) and the retrieved data string.</param>
        [UsedImplicitly]
        public static  void LoadFromCloud( string saveName, Action<bool, string> callback )
        {
            LoadFromCloudAsync(saveName).Run( res => callback?.Invoke( res.success, res.data ) );
        }

        /// <summary>
        /// Retrieves a data string from the Google Play Cloud Save service (Snapshots).
        /// </summary>
        /// <param name="saveName">The filename of the snapshot to read.</param>
        /// <param name="userToken"></param>
        [UsedImplicitly]
        public static async Awaitable<(bool success, string data)> LoadFromCloudAsync(string saveName, CancellationToken userToken = default)
        {
            using var linkedCts     = CancellationTokenSource.CreateLinkedTokenSource(userToken, GlobalToken);
            var       combinedToken = linkedCts.Token;
            
            // wait for any previous action to complete
            if( _semaphoreSignIn.CurrentCount == 0 )
                Log($"Waiting for GPGS state to stabilize before loading {saveName}...");
            await _semaphoreSignIn.WaitAsync(combinedToken);

            try
            {
                // user not connected
                if( !IsConnected )
                {
                    LogError("LoadFromCloud - User is not connected");
                    return (false, "not connected");
                }

                Log($"Calling LoadFromCloud: {saveName}");
                var       completionSource = new AwaitableCompletionSource<(bool, string)>();
                using var _                = completionSource.LinkToken( combinedToken );
                
                GPGSManager.GetInstance().LoadFromCloud(saveName, (success, data) => {
                    completionSource.SetResult((success, data));
                });

                return await completionSource.Awaitable;
            }
            finally
            {
                _semaphoreSignIn.Release();
            }
        }
    }
}
#endif