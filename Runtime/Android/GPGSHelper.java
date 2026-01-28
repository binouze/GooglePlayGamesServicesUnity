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

    public static void configure(String webClientId, boolean requestEmail, boolean requestProfile) {
        _webClientId    = webClientId;
        _requestEmail   = requestEmail;
        _requestProfile = requestProfile;
        logDebug("Configured. WebClientId set. RequestEmail: " + _requestEmail + " RequestProfile: " + _requestProfile);
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
                            // On renvoie SIGN_IN_REQUIRED (qui deviendra InvalidAccount/4 dans Unity)
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

    // --- PROCESSING RESULTS (Avec RequestServerSideAccess corrigé) ---

    private static void processSuccess() {
        Activity activity = UnityPlayer.currentActivity;

        // Préparation des scopes
        // Si _requestEmail est vrai, on ajoute le scope EMAIL
        // Sinon, on peut passer null ou une liste vide si on veut juste le code par défaut
        
        Task<?> task; // Task générique car le type de retour change selon la méthode appelée

        if (_requestEmail || _requestProfile ) {
            logDebug("Requesting Server Side Access WITH EMAIL scope...");
            List<AuthScope> scopes = new ArrayList<>();
            if( _requestEmail )
                scopes.add(AuthScope.EMAIL);
            if( _requestProfile )
                scopes.add(AuthScope.PROFILE);
            
            // Appel de la méthode à 3 arguments (Celle de ta documentation)
            // Retourne Task<AuthResponse>
            task = PlayGames.getGamesSignInClient(activity)
                .requestServerSideAccess(_webClientId, false, scopes);
        } else {
            logDebug("Requesting Server Side Access (No extra scopes)...");
            // Appel standard à 2 arguments
            // Retourne Task<String> (juste le code)
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

            final String finalAuthCode = authCode;

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
                });
        });
    }

    // --- SIGN OUT ---

    public static void signOut() {
        logDebug("SignOut");
        _isAuthenticated = false;
        sendResultToUnity(-2, null, null, null); 
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
}