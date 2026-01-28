package com.binouze;

import android.app.Activity;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import com.google.android.gms.common.api.ApiException;
import com.google.android.gms.common.api.CommonStatusCodes;

import com.google.android.gms.games.AuthenticationResult;
import com.google.android.gms.games.Player;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;
import com.google.android.gms.games.gamessignin.AuthScope;
import com.google.android.gms.games.gamessignin.AuthResponse;

import com.google.android.gms.tasks.Task;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

public class GPGSHelper {

    private static final String TAG = "GPGSHelper";
    private static final String CALLBACK_OBJECT = "GPGSManagerObject";
    private static final String CALLBACK_METHOD = "OnGPGSSignInResult";

    // --- CONFIGURATION STATE ---
    
    private static String  _webClientId     = "";
    private static boolean _requestEmail    = false; 
    private static boolean _requestAuthCode = false; 
    private static boolean _requestProfile  = false; 
    private static boolean _debugLog        = false;
    private static boolean _isAuthenticated = false;

    // --- LOGGING ---

    public static void enableDebugLogging(boolean enabled) {
        _debugLog = enabled;
    }

    private static void logDebug(String msg) {
        if (_debugLog) Log.d(TAG, ":: " + msg);
    }

    private static void logError(String msg) {
        Log.e(TAG, ":: " + msg);
    }

    // --- CONFIGURATION ---

    public static void configure(String webClientId, boolean requestAuthCode, boolean requestEmail, boolean requestProfile) {
        _webClientId     = webClientId;
        _requestEmail    = requestEmail;
        _requestAuthCode = requestAuthCode;
        _requestProfile  = requestProfile;
        logDebug("Configured. WebClientId set. RequestAuthCode: " + _requestAuthCode + " RequestEmail: " + _requestEmail + " RequestProfile: " + _requestProfile);
    }

    // --- SIGN IN FLOWS ---

    public static void signInSilently() {
        Activity activity = UnityPlayer.currentActivity;
        
        activity.runOnUiThread(() -> {
            PlayGamesSdk.initialize(activity);
    
            PlayGames.getGamesSignInClient(activity)
                .isAuthenticated()
                .addOnCompleteListener(task -> {
                    if (task.isSuccessful()) {
                        // La communication avec Google a réussi
                        AuthenticationResult result = task.getResult();
                        
                        if (result.isAuthenticated()) {
                            logDebug("Silent Sign-In Success!");
                            _isAuthenticated = true;
                            processSuccess();
                        } else {
                            // CAS 1 : Pas d'erreur technique, mais pas connecté.
                            // C'est le cas standard au premier lancement.
                            logDebug("Silent Sign-In: Not Authenticated (Normal).");
                            _isAuthenticated = false;
                            // On renvoie SIGN_IN_REQUIRED
                            int unityCode = mapToUnityStatusCode(CommonStatusCodes.SIGN_IN_REQUIRED);
                            sendResultToUnity(unityCode, null, null, null);
                        }
                    } else {
                        // CAS 2 : Erreur technique (Réseau, Config, etc.)
                        handleTaskException(task.getException(), "Silent Sign-In");
                    }
                });
        });
    }

    public static void signIn() {
        Activity activity = UnityPlayer.currentActivity;
    
        activity.runOnUiThread(() -> {
            PlayGamesSdk.initialize(activity);
            
            logDebug("Starting Interactive Sign-In...");
            PlayGames.getGamesSignInClient(activity)
                .signIn()
                .addOnCompleteListener(task -> {
                    if (task.isSuccessful()) {
                        AuthenticationResult result = task.getResult();
                        
                        if (result.isAuthenticated()) {
                            logDebug("Interactive Sign-In Success!");
                            _isAuthenticated = true;
                            processSuccess();
                        } else {
                            // CAS 1 : La fenêtre s'est ouverte mais le résultat est négatif
                            // Souvent assimilé à une annulation ou un échec silencieux
                            logDebug("Interactive Sign-In: Not Authenticated (No Exception).");
                            _isAuthenticated = false;
                            int unityCode = mapToUnityStatusCode(CommonStatusCodes.CANCELED);
                            sendResultToUnity(unityCode, null, null, null);
                        }
                    } else {
                        // CAS 2 : Vraie erreur (Crash, Config SHA-1 incorrecte, etc.)
                        handleTaskException(task.getException(), "Interactive Sign-In");
                    }
                });
        });
    }

    private static void handleTaskException(Exception exception, String context) {
        int googleCode = CommonStatusCodes.ERROR;
        String errorMessage = "Unknown Error";
        _isAuthenticated = false;
    
        if (exception instanceof ApiException) {
            ApiException apiException = (ApiException) exception;
            googleCode = apiException.getStatusCode();
            errorMessage = apiException.getMessage();
        } else if (exception != null) {
            errorMessage = exception.toString();
        }
    
        int unityCode = mapToUnityStatusCode(googleCode);
        
        logError(context + " Failed. StatusCode: " + googleCode + " -> UnityCode: " + unityCode + " | Message: " + errorMessage);
        
        sendResultToUnity(unityCode, null, null, null);
    }

    // --- PROCESSING RESULTS ---

    private static void processSuccess() {
        Activity activity = UnityPlayer.currentActivity;

        // Si on ne demande pas d'AuthCode, on passe directement à la récupération du profil
        if (!_requestAuthCode) {
            logDebug("AuthCode not requested, fetching player info directly...");
            fetchPlayerInfo(""); // On passe un code vide
            return;
        }

        // Sinon, on procède à la demande d'accès serveur
        Task<?> task; 
        if( _requestEmail || _requestProfile ) {
            logDebug("Requesting Server Side Access with custom scope...");
            List<AuthScope> scopes = new ArrayList<>();
            if( _requestEmail )
                scopes.add(AuthScope.EMAIL);
            if( _requestProfile )
                scopes.add(AuthScope.PROFILE);
            
            task = PlayGames.getGamesSignInClient(activity)
                .requestServerSideAccess(_webClientId, false, scopes);
        } else {
            logDebug("Requesting Server Side Access (No extra scopes)...");
            task = PlayGames.getGamesSignInClient(activity)
                .requestServerSideAccess(_webClientId, false);
        }

        task.addOnCompleteListener(t -> {
            String authCode = "";
            
            if (t.isSuccessful()) {
                Object result = t.getResult();
                
                // On gère les deux types de retours possibles
                if (result instanceof AuthResponse) {
                    authCode = ((AuthResponse) result).getAuthCode(); //
                } else if (result instanceof String) {
                    authCode = (String) result;
                }
                
                logDebug("Got AuthCode: " + authCode);
            } else {
                logError("Failed to get AuthCode");
            }

            // récupérer les infos GooglePlayGames 
            fetchPlayerInfo(authCode);
            /*final String finalAuthCode = authCode;

            // 2. Récupérer le GamerTag (Pseudo)
            PlayGames.getPlayersClient(activity).getCurrentPlayer()
                .addOnCompleteListener(playerTask -> {
                    String displayName = "Player";
                    String gpgsId      = "";
                    if (playerTask.isSuccessful() && playerTask.getResult() != null) {
                        Player player = playerTask.getResult();
                        displayName   = player.getDisplayName();
                        gpgsId        = player.getPlayerId();
                        logDebug("Got Player Info. Name: " + displayName + ", ID: " + gpgsId);
                    }
                    else{
                        logError("Failed to get Player Object");
                    }
                    
                    // 3. Envoyer le JSON final
                    sendResultToUnity(0, finalAuthCode, displayName, gpgsId);
                });*/
        });
    }
    
    private static void fetchPlayerInfo(String authCode) {
        Activity activity = UnityPlayer.currentActivity;
        
        PlayGames.getPlayersClient(activity).getCurrentPlayer()
            .addOnCompleteListener(playerTask -> {
                String displayName = "Player";
                String gpgsId      = "";
                if (playerTask.isSuccessful() && playerTask.getResult() != null) {
                    Player player = playerTask.getResult();
                    displayName   = player.getDisplayName();
                    gpgsId        = player.getPlayerId();
                    logDebug("Got Player Info. Name: " + displayName + ", ID: " + gpgsId);
                } else {
                    logError("Failed to get Player Object");
                }
                
                sendResultToUnity(0, authCode, displayName, gpgsId);
            });
    }

    // --- SIGN OUT ---

    public static void signOut() {
        logDebug("SignOut");
        _isAuthenticated = false;
        sendResultToUnity(-2, null, null, null); 
    }
   

    public static void closeDialog() {
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            try {
                logDebug("Force closing GPGS windows...");
                activity.finishActivity(9003); 
            } catch (Exception e) {
                logError("Error closing dialogs: " + e.getMessage());
            }
        });
    }

    // --- JSON HELPER ---

    private static void sendResultToUnity(int status, String authCode, String displayName, String gpgsId) {
        try {
            JSONObject jsonRoot = new JSONObject();
            JSONObject jsonResult = new JSONObject();

            jsonResult.put("Status", status);

            if (status == 0) { // Success
                jsonResult.put("AuthCode",    authCode != null    ? authCode    : "");
                jsonResult.put("DisplayName", displayName != null ? displayName : "");
                jsonResult.put("GPGSId",      gpgsId != null      ? gpgsId      : "");
                jsonResult.put("UserId", ""); 
                jsonResult.put("Email", "");
                jsonResult.put("GivenName", "");
                jsonResult.put("FamilyName", "");
                jsonResult.put("PhotoUrl", "");
            }

            jsonRoot.put("result", jsonResult);
            UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD, jsonRoot.toString());

        } catch (JSONException e) {
            logError("JSON Error: " + e.getMessage());
            UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD, "{\"result\":{\"Status\":9}}");
        }
    }
    
    
    // --- ERROR MAPPING HELPER ---
    
    private static int mapToUnityStatusCode(int googleCode) {
        switch (googleCode) {
            case CommonStatusCodes.SUCCESS:              // 0
                return 0; // Success

            case CommonStatusCodes.API_NOT_CONNECTED:    // 17
                return 1; // ApiNotConnected

            case CommonStatusCodes.CANCELED:             // 16
                return 2; // Canceled

            case CommonStatusCodes.INTERRUPTED:          // 14
                return 3; // Interrupted

            case CommonStatusCodes.SIGN_IN_REQUIRED:     // 4
            case CommonStatusCodes.INVALID_ACCOUNT:      // 5
            case 12501: // SIGN_IN_CANCELLED (spécifique Google Sign In)
                return 4; // InvalidAccount (ou Auth Failed)

            case CommonStatusCodes.TIMEOUT:              // 15
                return 5; // Timeout

            case CommonStatusCodes.DEVELOPER_ERROR:      // 10 (Erreur SHA-1 classique)
                return 6; // DeveloperError

            case CommonStatusCodes.INTERNAL_ERROR:       // 8
                return 7; // InternalError (Attention: Votre Enum est 7, Google est 8)

            case CommonStatusCodes.NETWORK_ERROR:        // 7
                return 8; // NetworkError (Attention: Votre Enum est 8, Google est 7)

            default:
                logDebug("Unmapped Google Error Code: " + googleCode);
                return 9; // Error (Generic)
        }
    }
        
        
    // --- ACHIEVEMENTS ---
    
    public static void unlockAchievement(String achievementId) {
        if( !_isAuthenticated )
        {
            logDebug("Unlock Achievement FAIL, user not signed in");
            return;
        }
    
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            try {
                PlayGames.getAchievementsClient(activity).unlock(achievementId);
                logDebug("Unlock Achievement: " + achievementId);
            } catch (Exception e) {
                logError("Unlock failed: " + e.getMessage());
            }
        });
    }

    public static void showAchievements() {
        if( !_isAuthenticated )
        {
            logDebug("Show Achievements FAIL, user not signed in");
            return;
        }
                
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            PlayGames.getAchievementsClient(activity)
                .getAchievementsIntent()
                .addOnSuccessListener(intent -> {
                    activity.startActivityForResult(intent, 9003);
                })
                .addOnFailureListener(e -> logError("Show UI failed: " + e.getMessage()));
        });
    }
    
    public static void incrementAchievement(String achievementId, int steps) {
        if (!_isAuthenticated) {
            logDebug("Increment Achievements FAIL, user not signed in");
            return;
        }
    
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            try {
                // .increment() ajoute 'steps' au total déjà stocké sur les serveurs Google
                PlayGames.getAchievementsClient(activity).increment(achievementId, steps);
                logDebug("Incremented Achievement: " + achievementId + " by " + steps + " steps.");
            } catch (Exception e) {
                logError("Increment failed: " + e.getMessage());
            }
        });
    }
    
    public static void setStepAchievement(String achievementId, int steps) {
        if (!_isAuthenticated) {
            logDebug("SetStep Achievements FAIL, user not signed in");
            return;
        }
    
        Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            try {
                // .setStep() défnini 'steps' sur les serveurs Google pour cet achievement
                PlayGames.getAchievementsClient(activity).setStepsImmediate(achievementId, steps);
                logDebug("SetStep Achievement: " + achievementId + " set " + steps + " steps.");
            } catch (Exception e) {
                logError("SetStep failed: " + e.getMessage());
            }
        });
    }
}