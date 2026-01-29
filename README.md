# Google Play Games Services (GPGS) Unity Plugin

A lightweight, native bridge between Unity and the Google Play Games Services SDK for Android. This plugin provides a simplified interface for authentication, achievements, and cloud saving.

## 🚀 Quick Start

### 1. Installation:

- in the package manager, click on the +
- select `add package from GIT url`
- paste the following url: `"https://github.com/binouze/GooglePlayGamesServicesUnity.git"`

### 2. Configuration

- Go to the menu `LagoonPlugins/GooglePlayGames Settings` and fill at least the GPGS_ID with the GooglePlayGame ID found in the Google Play Console.
- If you need to request AuthCode, you will need to have the OAuthClientID too.
- By default, the SignIn requests the AuthCode, Email and Profile. If you don't need this, you can set these to false.
- Note that you will need to call the google API from a server to get the profile and email information using the AuthCode.

```csharp
// by default the 3 values are true, you can change it before calling the SignIn method
GooglePlayServices.SetConfiguration(new GPGSConfiguration {
    RequestEmail    = false,
    RequestProfile  = false,
    RequestAuthCode = false
});
```


### 3. Authentication
Use the SignIn method to connect users. It supports both silent background checks and interactive UI flows.

```csharp
GooglePlayServices.SignIn(() => 
{
    if( GooglePlayServices.IsConnected ) 
    {
        Debug.Log($"Authenticated as: {GooglePlayServices.User.DisplayName}");
    } 
    else 
    {
        Debug.Log($"Sign-in failed. Status: {GooglePlayServices.LastSignInStatus}");
    }
}, silent: true);
```
---

## 🏆 Achievements
Manage player progression with native calls to the Google Play Games SDK.

* **Unlock**: Instantly grant an achievement by its ID.
```csharp
GooglePlayServices.UnlockAchievement( "YOUR_ACHIEVEMENT_ID" );
```
* **Increment**: Add a relative number of steps to an incremental achievement.
```csharp
GooglePlayServices.IncrementAchievement( "YOUR_ACHIEVEMENT_ID", 5 );
```
* **Set Steps**: Set an achievement's progress to a specific absolute value.
```csharp
GooglePlayServices.SetStepsAchievement( "YOUR_ACHIEVEMENT_ID", 100 );
```
* **Show UI**: Open the native Google Play Games overlay to view all achievements.
```csharp
GooglePlayServices.ShowAchievementsUI();
```
---

## ☁️ Cloud Save (Snapshots)
Save and load game data strings (JSON/Plain text) using Google's Snapshot API.

### Save Data
```csharp
GooglePlayServices.SaveToCloud("MySaveFile", gameData, (success) => 
{
    Debug.Log( success ? "Saved successfully!" : "Save failed." );
});
```
### Load Data
```csharp
GooglePlayServices.LoadFromCloud("MySaveFile", (success, data) => 
{
    if( success ) 
    {
        Debug.Log("Loaded data: " + data);
    }
    else
    {
        Debug.Log("Load Failed");
    }
});
```
---

## 🛠️ Components Reference

| Class | Description |
| :--- | :--- |
| GooglePlayServices | The main static entry point for all features. |
| GPGSUser | Holds data for the authenticated user such as DisplayName, ID, and AuthCode. |
| GPGSSettings | ScriptableObject storing IDs and Auto-SignIn preferences. |
| GPGSHelper (Java) | Native layer communicating directly with the GMS Play Games SDK. |

## 💻 Editor Support
The plugin includes an EditorHelper that provides a mock interface for sign-in. When running in the Unity Editor, it will prompt you for a fake User ID and GPGS ID so you can test your game logic without an Android device.

```csharp
// you can enable logging from the plugin to debug more easilly
GooglePlayServices.SetLoggingEnabled(true);
```
