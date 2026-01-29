package com.binouze;

import android.app.Activity;
import android.util.Log;
import android.app.Instrumentation;
import android.os.Handler;
import android.os.Looper;

import com.unity3d.player.UnityPlayer;

// GMS Games
import com.google.android.gms.games.AuthenticationResult;
import com.google.android.gms.games.Player;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;
import com.google.android.gms.games.gamessignin.AuthScope;
import com.google.android.gms.games.gamessignin.AuthResponse;

// GMAS Snapshots
import com.google.android.gms.games.SnapshotsClient;
import com.google.android.gms.games.snapshot.Snapshot;
import com.google.android.gms.games.snapshot.SnapshotMetadataChange;
import com.google.android.gms.games.snapshot.SnapshotMetadata;

// GMS Auth & Common
import com.google.android.gms.common.api.ApiException;
import com.google.android.gms.common.api.CommonStatusCodes;
import com.google.android.gms.tasks.Task;

// JSON
import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

public class GPGSHelper {

    private static final String TAG                              = "GPGSHelper";
    private static final String CALLBACK_OBJECT                  = "GPGSManagerObject";
    private static final String CALLBACK_METHOD_SIGN_IN          = "OnGPGSSignInResult";
    private static final String CALLBACK_METHOD_CLOUD_SAVE_READ  = "OnGPGSCloudSaveReadResult";
    private static final String CALLBACK_METHOD_CLOUD_SAVE_WRITE = "OnGPGSCloudSaveWriteResult";

    // --- CONFIGURATION STATE ---
    
    private static String  _webClientId     = "";
    private static boolean _requestEmail    = false; 
    private static boolean _requestAuthCode = false; 
    private static boolean _requestProfile  = false; 
    private static boolean _debugLog        = false;
    private static boolean _usingProvider   = true;
    
    // Etat Runtime
    private static boolean _isAuthenticated = false;
    private static boolean _isInitialized   = false;

    // --- LOGGING ---

    public static void enableDebugLogging(boolean enabled) {
        _debugLog = enabled;
    }

    private static void logDebug(String msg) {
        if (_debugLog) Log.d(TAG, "["+TAG+"] " + msg);
    }

    private static void logError(String msg) {
        Log.e(TAG, "["+TAG+"] " + msg);
    }

    // --- CONFIGURATION ---

    public static void configure( String webClientId, boolean requestAuthCode, boolean requestEmail, boolean requestProfile, boolean autologinEnabled ) {
        _webClientId     = webClientId;
        _requestEmail    = requestEmail;
        _requestAuthCode = requestAuthCode;
        _requestProfile  = requestProfile;
        
        // Si autologinEnabled, le SDK est déjà initialisé par le Provider au démarrage de l'app.
        _usingProvider = autologinEnabled;
        if(_usingProvider) {
            _isInitialized = true;
        }

        logDebug("Configured. WebClientId set. ProviderEnabled: " + _usingProvider);
    }


    // -------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---                                                         S I G N   I N   /   S I G N   O U T                                                                 ---
    // -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // --- SIGN IN FLOWS ---
    
    public static void signInSilently(boolean force) {
        Activity activity = UnityPlayer.currentActivity;
        
        activity.runOnUiThread(() -> {
            // initialisation du SDK si c'est pas déjà fait
            // dans ce cas la on attendra un petit peu avant de faire la suite
            boolean needsDelay = initializeSdk(activity);
    
            logDebug("Starting Silent Sign-In...");
    
            // définition de la tâche de connexion
            Runnable authTask = () -> {
                PlayGames.getGamesSignInClient(activity)
                    .isAuthenticated()
                    .addOnCompleteListener(task -> {
                        if (task.isSuccessful()) {
                            AuthenticationResult result = task.getResult();
                            if (result.isAuthenticated()) {
                                logDebug("Silent Sign-In Success!");
                                _isAuthenticated = true;
                                processSignInSuccess();
                            } else {
                                logDebug("Silent Sign-In: Not Authenticated.");
                                _isAuthenticated = false;
                                int unityCode    = mapToUnityStatusCode(CommonStatusCodes.SIGN_IN_REQUIRED);
                                sendSignInResultToUnity(unityCode, null, null, null);
                            }
                        } else {
                            handleSignInException(task.getException(), "Silent Sign-In");
                        }
                    });
            };

            // Si l'init était déjà faite       -> exécution immédiate
            // Si on vient d'initialiser le SDK -> exécution différée pour laisser le temps au hack init
            if (!needsDelay) {
                authTask.run();
            } else {
                new Handler(Looper.getMainLooper()).postDelayed(authTask, 500);
            }
        });
    }

    public static void signIn() {
        Activity activity = UnityPlayer.currentActivity;
    
        activity.runOnUiThread(() -> {
            // initialisation du SDK si c'est pas déjà fait
            // dans ce cas la on attendra un petit peu avant de faire la suite
            boolean needsDelay = initializeSdk(activity);
            
            logDebug("Starting Interactive Sign-In...");
            
            // définition de la tâche de connexion
            Runnable signInTask = () -> {
                PlayGames.getGamesSignInClient(activity)
                    .signIn()
                    .addOnCompleteListener(task -> {
                        if (task.isSuccessful()) {
                            AuthenticationResult result = task.getResult();
                            if (result.isAuthenticated()) {
                                logDebug("Interactive Sign-In Success!");
                                _isAuthenticated = true;
                                processSignInSuccess();
                            } else {
                                logDebug("Interactive Sign-In: Canceled or Failed.");
                                _isAuthenticated = false;
                                int unityCode    = mapToUnityStatusCode(CommonStatusCodes.CANCELED);
                                sendSignInResultToUnity(unityCode, null, null, null);
                            }
                        } else {
                            handleSignInException(task.getException(), "Interactive Sign-In");
                        }
                    });
            };

            // Si l'init était déjà faite       -> exécution immédiate
            // Si on vient d'initialiser le SDK -> exécution différée pour laisser le temps au hack init
            if (!needsDelay) {
                signInTask.run();
            } else {
                new Handler(Looper.getMainLooper()).postDelayed(signInTask, 500);
            }
        });
    }

    // --- PROCESSING RESULTS ---
    
    private static void handleSignInException(Exception exception, String context) {
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
        
        sendSignInResultToUnity(unityCode, null, null, null);
    }

    private static void processSignInSuccess() {
        Activity activity = UnityPlayer.currentActivity;

        // si on ne demande pas d'AuthCode, on passe directement à la récupération du profil
        if (!_requestAuthCode) {
            logDebug("AuthCode not requested, fetching player info directly...");
            fetchPlayerInfo(""); // On passe un code vide
            return;
        }

        // sinon, on procède à la demande d'accès serveur
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
                    authCode = ((AuthResponse) result).getAuthCode();
                } else if (result instanceof String) {
                    authCode = (String) result;
                }
                
                logDebug("Got AuthCode: " + authCode);
            } else {
                logError("Failed to get AuthCode");
            }

            // récupérer les infos GooglePlayGames 
            fetchPlayerInfo(authCode);
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
                
                sendSignInResultToUnity(0, authCode, displayName, gpgsId);
            });
    }
    
    private static void sendSignInResultToUnity(int status, String authCode, String displayName, String gpgsId) {
        try {
            JSONObject jsonRoot   = new JSONObject();
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
            UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD_SIGN_IN, jsonRoot.toString());

        } catch (JSONException e) {
            logError("JSON Error: " + e.getMessage());
            UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD_SIGN_IN, "{\"result\":{\"Status\":9}}");
        }
    }

    // --- SIGN OUT ---

    public static void signOut() {
        logDebug("SignOut");
        _isAuthenticated = false;
        sendSignInResultToUnity(-2, null, null, null); 
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
    
    // --- INIT ---
    
    // manually initialize the SDK if the autoSign in is disabled 
    private static boolean initializeSdk(Activity activity) {
        if (_isInitialized) return false;
    
        PlayGamesSdk.initialize(activity);
        _isInitialized = true;
    
        // Si on utilise le Provider, pas besoin de hack.
        // On ne fait le hack Instrumentation que si le Provider a été supprimé.
        if (!_usingProvider) {
            try {
                logDebug("No Provider detected. Forcing Lifecycle events via Instrumentation...");
                Instrumentation instrum = new Instrumentation();
                instrum.callActivityOnPause(activity);
                // On laisse 100ms au système pour "digérer" la pause
                new android.os.Handler(android.os.Looper.getMainLooper()).postDelayed(() -> {
                    try {
                        instrum.callActivityOnResume(activity);
                    } catch (Exception e) {
                        logError("Instrumentation hack failed 2: " + e.getMessage());
                    }
                }, 100);
            } catch (Exception e) {
                logError("Instrumentation hack failed 1: " + e.getMessage());
            }
        }
        
        return true;
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
    
    // -------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---                                                               A C H I E V E M E N T S                                                                       ---
    // -------------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        
    // --- UNLOCK ---
    
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

    // --- SHOW ---

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
    
    // --- INCREMENT ---
    
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
    
    // --- SET STEP ---
    
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
    
    // -------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // ---                                                               C L O U D   S A V E                                                                           ---
    // -------------------------------------------------------------------------------------------------------------------------------------------------------------------


    // --- CLOUD SAVE: WRITE ---

    public static void setCloudSaveDatas(String saveFileName, String data) {
        Activity activity = UnityPlayer.currentActivity;
        
        activity.runOnUiThread(() -> {
            // 1. Accès au client Snapshots
            SnapshotsClient snapshotsClient = PlayGames.getSnapshotsClient(activity);

            // 2. Ouverture du fichier (Ou création si inexistant)
            // RESOLUTION_POLICY_MOST_RECENTLY_MODIFIED : En cas de conflit, on garde le plus récent.
            snapshotsClient.open(saveFileName, true, SnapshotsClient.RESOLUTION_POLICY_MOST_RECENTLY_MODIFIED)
                .addOnCompleteListener(task -> {
                    if (!task.isSuccessful()) {
                        logError("Cloud Save: Open Failed. " + task.getException());
                        sendCloudSaveStatusToUnity("-1");
                        return;
                    }

                    // 3. Le fichier est ouvert, on récupère le Snapshot
                    Snapshot snapshot = task.getResult().getData();
                    
                    if (snapshot == null) {
                        logError("Cloud Save: Snapshot is null despite success.");
                        sendCloudSaveStatusToUnity("-3");
                        return;
                    }

                    // 4. Écriture des données
                    try {
                        // On écrit les nouvelles données
                        snapshot.getSnapshotContents().writeBytes(data.getBytes(StandardCharsets.UTF_8));

                        // 5. Commit (Validation et Fermeture)
                        SnapshotMetadataChange metadataChange = new SnapshotMetadataChange.Builder()
                                .setDescription("Auto-save")
                                .build();

                        snapshotsClient.commitAndClose(snapshot, metadataChange)
                                .addOnCompleteListener(commitTask -> {
                                    if (commitTask.isSuccessful()) {
                                        logDebug("Cloud Save: SUCCESS!");
                                        sendCloudSaveStatusToUnity("0");
                                    } else {
                                        logError("Cloud Save: Commit Failed. " + commitTask.getException());
                                        sendCloudSaveStatusToUnity("-4");
                                    }
                                });

                    } catch (IOException e) {
                        logError("Cloud Save: IO Error. " + e.getMessage());
                        sendCloudSaveStatusToUnity("-2");
                    }
                });
        });
    }

    // --- CLOUD SAVE: READ ---

    public static void getCloudSaveDatas(String saveFileName) {
        Activity activity = UnityPlayer.currentActivity;

        activity.runOnUiThread(() -> {
            SnapshotsClient snapshotsClient = PlayGames.getSnapshotsClient(activity);

            // 1. Ouverture en mode lecture
            snapshotsClient.open(saveFileName, true, SnapshotsClient.RESOLUTION_POLICY_MOST_RECENTLY_MODIFIED)
                .addOnCompleteListener(task -> {
                    if (!task.isSuccessful()) {
                        logError("Cloud Load: Open Failed. " + task.getException());
                        // On renvoie le code d'erreur -1
                        sendCloudDataToUnity("-1"); 
                        return;
                    }

                    // 2. Récupération du Snapshot
                    Snapshot snapshot = task.getResult().getData(); // getData() gère le conflit automatiquement ici
                    
                    // Cas spécial : Le fichier vient d'être créé mais est vide
                    if (snapshot == null) {
                        sendCloudDataToUnity("");
                        return;
                    }

                    // 3. Lecture des données
                    try {
                        byte[] rawData = snapshot.getSnapshotContents().readFully();
                        if (rawData != null && rawData.length > 0) {
                            String dataStr = new String(rawData, StandardCharsets.UTF_8);
                            logDebug("Cloud Load: SUCCESS. Data len: " + dataStr.length());
                            sendCloudDataToUnity(dataStr);
                        } else {
                            logDebug("Cloud Load: File is empty (New game).");
                            sendCloudDataToUnity("");
                        }
                    } catch (IOException e) {
                        logError("Cloud Load: IO Exception. " + e.getMessage());
                        sendCloudDataToUnity("-2");
                    }
                });
        });
    }
    
    // retourner les données CloudSave au client Unity 
    private static void sendCloudDataToUnity(String data) {
        UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD_CLOUD_SAVE_READ, data);
    }
    // retourner le status de la sauvegarde CloudSave au client Unity
    private static void sendCloudSaveStatusToUnity(String data) {
        UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, CALLBACK_METHOD_CLOUD_SAVE_WRITE, data);
    }
}