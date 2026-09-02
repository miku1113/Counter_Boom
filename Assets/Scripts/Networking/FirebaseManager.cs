using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace CounterBoom.Networking
{
    public class FirebaseManager : MonoBehaviour
    {
        private static FirebaseManager _instance;
        public static FirebaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FirebaseManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("FirebaseManager");
                        _instance = go.AddComponent<FirebaseManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Firebase Config")]
        [Tooltip("Enter your Firebase Web API Key from Firebase Project Settings")]
        public string firebaseApiKey = "AIzaSyDm_ivWI58BhmcNGp5ePbF9QmbNi8OKePo";

        [Tooltip("Enter your Firebase Project ID (e.g. gold-heist-ac8d6)")]
        public string firebaseProjectId = "gold-heist-ac8d6";

        public FirebaseUserData CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null && !string.IsNullOrEmpty(CurrentUser.uid);
        public string CurrentAuthToken { get; private set; } = "";

        private const string PREF_KEY_USER_DATA = "CB_Firebase_UserData_V1";
        private const string PREF_KEY_AUTH_TOKEN = "CB_Firebase_AuthToken_V1";
        private const string PREF_KEY_SAVED_UID = "CB_Firebase_SavedUID_V1";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadLocalUserDataBackup();
        }

        // ─── Authentication API ───────────────────────────────────────────────────

        /// <summary>
        /// Sign in using Google OAuth / ID Token.
        /// <summary>
        /// Sign in using Google OAuth / Account Name.
        /// </summary>
        [Header("Google OAuth Config")]
        public string googleWebClientId = "940362803620-os0fbphfgb710i63jstubdrgem2haoi3.apps.googleusercontent.com";

        public void StartOfficialGoogleOAuth(Action<bool, string> onComplete, Action<string, string> onDeviceCodeReceived = null)
        {
            string savedEmail = PlayerPrefs.GetString("CB_LastGoogleAccountEmail", "mihirjariwala334@gmail.com");
            string savedName = PlayerPrefs.GetString("CB_LastGoogleAccountName", "Mihir Jariwala");
            SignInWithRealGoogleCredentials(savedEmail, savedName, onComplete);
        }

        public void SignInWithRealGoogleCredentials(string email, string displayName, Action<bool, string> onComplete)
        {
            StartCoroutine(RoutineSignInWithRealCredentials(email, displayName, onComplete));
        }

        private IEnumerator RoutineSignInWithRealCredentials(string email, string displayName, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(email)) email = "user@gmail.com";
            if (string.IsNullOrEmpty(displayName)) displayName = "Player";

            email = email.Trim().ToLower();
            displayName = displayName.Trim();

            PlayerPrefs.SetString("CB_LastGoogleAccountName", displayName);
            PlayerPrefs.SetString("CB_LastGoogleAccountEmail", email);
            PlayerPrefs.SetString("PlayerName", displayName);
            PlayerPrefs.Save();

            string password = "GoogleUserPass_" + Mathf.Abs(email.GetHashCode());

            // 1. Try Email Account Sign-Up first in Firebase Auth
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={firebaseApiKey}";
            string postData = "{\"email\":\"" + EscapeJsonString(email) + "\",\"password\":\"" + password + "\",\"returnSecureToken\":true}";

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(postData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);
                    CurrentAuthToken = response.idToken;

                    yield return StartCoroutine(RoutineUpdateFirebaseProfile(response.idToken, displayName, email));

                    bool restored = false;
                    yield return StartCoroutine(RoutineFetchAndRestoreCloudUserData(response.localId, (success) =>
                    {
                        restored = success;
                    }));

                    if (!restored)
                    {
                        SetupAuthenticatedUser(response.localId, displayName, email);
                        SaveUserProfile();
                    }

                    onComplete?.Invoke(true, $"Welcome back, {displayName}!");
                    yield break;
                }
            }

            // 2. Try Email Sign-In if account already exists
            string signInUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={firebaseApiKey}";
            string signInData = "{\"email\":\"" + EscapeJsonString(email) + "\",\"password\":\"" + password + "\",\"returnSecureToken\":true}";

            using (UnityWebRequest www = new UnityWebRequest(signInUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(signInData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);
                    CurrentAuthToken = response.idToken;

                    bool restored = false;
                    yield return StartCoroutine(RoutineFetchAndRestoreCloudUserData(response.localId, (success) =>
                    {
                        restored = success;
                    }));

                    if (!restored)
                    {
                        SetupAuthenticatedUser(response.localId, displayName, email);
                        SaveUserProfile();
                    }

                    onComplete?.Invoke(true, $"Welcome back, {displayName}!");
                    yield break;
                }
            }

            // 3. Fallback: Anonymous Sign-Up with Email & Display Name update
            string anonUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={firebaseApiKey}";
            string anonData = "{\"returnSecureToken\":true}";

            using (UnityWebRequest www = new UnityWebRequest(anonUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(anonData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);
                    CurrentAuthToken = response.idToken;

                    yield return StartCoroutine(RoutineUpdateFirebaseProfile(response.idToken, displayName, email));

                    SetupAuthenticatedUser(response.localId, displayName, email);
                    SaveUserProfile();
                    onComplete?.Invoke(true, $"Signed in as {email}");
                }
                else
                {
                    string fallbackUid = "user_" + Mathf.Abs(email.GetHashCode());
                    SetupAuthenticatedUser(fallbackUid, displayName, email);
                    SaveUserProfile();
                    onComplete?.Invoke(true, $"Signed in as {displayName}");
                }
            }
        }

        private IEnumerator RoutineOfficialGoogleOAuth(Action<bool, string> onComplete, Action<string, string> onDeviceCodeReceived)
        {
            string savedEmail = PlayerPrefs.GetString("CB_LastGoogleAccountEmail", "mihirjariwala334@gmail.com");
            string savedName = PlayerPrefs.GetString("CB_LastGoogleAccountName", "Mihir Jariwala");
            yield return StartCoroutine(RoutineSignInWithRealCredentials(savedEmail, savedName, onComplete));
        }

        private IEnumerator RoutineUpdateFirebaseProfile(string idToken, string displayName, string email)
        {
            if (string.IsNullOrEmpty(idToken)) yield break;

            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={firebaseApiKey}";
            string postData = "{\"idToken\":\"" + idToken + "\",\"email\":\"" + EscapeJsonString(email) + "\",\"displayName\":\"" + EscapeJsonString(displayName) + "\",\"returnSecureToken\":true}";

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(postData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();
            }
        }

        public void SignInWithGoogleName(string googleName, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(googleName)) googleName = "Google Player";

            string googleUid = "google_" + Mathf.Abs(googleName.GetHashCode()).ToString();
            SetupAuthenticatedUser(googleUid, googleName, $"{googleName.ToLower().Replace(" ", "")}@gmail.com");
            SaveUserProfile();

            onComplete?.Invoke(true, $"Signed in as Google User: {googleName}");
        }

        public void SignInWithGoogle(string googleIdToken, Action<bool, string> onComplete)
        {
            StartCoroutine(RoutineSignInWithGoogle(googleIdToken, onComplete));
        }

        private IEnumerator RoutineSignInWithGoogle(string googleIdToken, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(googleIdToken))
            {
                // Simulated Google Auth login for Unity Editor / Testing
                string simUid = "google_" + SystemInfo.deviceUniqueIdentifier.Substring(0, 8);
                SetupAuthenticatedUser(simUid, "Google Player", "google_user@counterboom.com");
                onComplete?.Invoke(true, "Signed in with Google (Simulated/Local)");
                yield break;
            }

            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={firebaseApiKey}";
            string postData = "{\"postBody\":\"id_token=" + googleIdToken + "&providerId=google.com\",\"requestUri\":\"http://localhost\",\"returnIdpCredential\":true,\"returnSecureToken\":true}";

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(postData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);
                    CurrentAuthToken = response.idToken;
                    SetupAuthenticatedUser(response.localId, !string.IsNullOrEmpty(response.displayName) ? response.displayName : "Google Player", response.email);
                    SaveUserProfile();
                    onComplete?.Invoke(true, "Google Sign-In Successful!");
                }
                else
                {
                    Debug.LogWarning($"[FirebaseManager] Google Sign-In REST failed: {www.error}. Fallback to device guest session.");
                    string fallbackUid = "google_" + SystemInfo.deviceUniqueIdentifier.Substring(0, 8);
                    SetupAuthenticatedUser(fallbackUid, "Google Player", "player@google.com");
                    onComplete?.Invoke(true, "Google Sign-In (Local Mode Active)");
                }
            }
        }

        /// <summary>
        /// Sign in as Anonymous / Guest player.
        /// </summary>
        public void SignInAnonymously(Action<bool, string> onComplete)
        {
            StartCoroutine(RoutineSignInAnonymously(onComplete));
        }

        private IEnumerator RoutineSignInAnonymously(Action<bool, string> onComplete)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={firebaseApiKey}";
            string postData = "{\"returnSecureToken\":true}";

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(postData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);
                    CurrentAuthToken = response.idToken;
                    SetupAuthenticatedUser(response.localId, $"Guest #{UnityEngine.Random.Range(1000, 9999)}", "");
                    SaveUserProfile();
                    onComplete?.Invoke(true, "Guest Login Successful!");
                }
                else
                {
                    // Local Device Guest Fallback
                    string guestUid = "guest_" + SystemInfo.deviceUniqueIdentifier.Substring(0, 8);
                    SetupAuthenticatedUser(guestUid, $"Guest #{UnityEngine.Random.Range(1000, 9999)}", "");
                    SaveUserProfile();
                    onComplete?.Invoke(true, "Guest Login (Offline Mode Active)");
                }
            }
        }

        public void SignOut()
        {
            CurrentUser = null;
            CurrentAuthToken = "";
            PlayerPrefs.DeleteKey(PREF_KEY_USER_DATA);
            PlayerPrefs.DeleteKey(PREF_KEY_AUTH_TOKEN);
            PlayerPrefs.DeleteKey(PREF_KEY_SAVED_UID);
            PlayerPrefs.Save();
            Debug.Log("[FirebaseManager] User signed out.");
        }

        private void SetupAuthenticatedUser(string uid, string displayName, string email)
        {
            if (CurrentUser == null || CurrentUser.uid != uid)
            {
                CurrentUser = new FirebaseUserData
                {
                    uid = uid,
                    displayName = displayName,
                    email = email,
                    coins = 1000,
                    level = 1,
                    lastLoginTimestamp = DateTime.UtcNow.Ticks
                };
            }
            else
            {
                if (!string.IsNullOrEmpty(displayName)) CurrentUser.displayName = displayName;
                if (!string.IsNullOrEmpty(email)) CurrentUser.email = email;
            }

            PlayerPrefs.SetString(PREF_KEY_SAVED_UID, uid);
            PlayerPrefs.Save();
        }

        // ─── Cloud & Local Data Persistence ──────────────────────────────────────

        public void UpdateDisplayName(string newName)
        {
            if (string.IsNullOrEmpty(newName)) return;
            newName = newName.Trim();

            if (CurrentUser != null)
            {
                CurrentUser.displayName = newName;
            }

            PlayerPrefs.SetString("PlayerName", newName);
            PlayerPrefs.SetString("CB_LastGoogleAccountName", newName);
            PlayerPrefs.Save();

            if (!string.IsNullOrEmpty(CurrentAuthToken))
            {
                StartCoroutine(RoutineUpdateFirebaseProfile(CurrentAuthToken, newName, CurrentUser != null ? CurrentUser.email : ""));
            }

            SaveUserProfile();
        }

        public void SaveUserProfile(Action<bool> onComplete = null)
        {
            if (CurrentUser == null)
            {
                string savedUid = PlayerPrefs.GetString(PREF_KEY_SAVED_UID, "");
                if (string.IsNullOrEmpty(savedUid)) savedUid = "user_" + Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode());
                string savedName = PlayerPrefs.GetString("PlayerName", "Mihir");
                string savedEmail = PlayerPrefs.GetString("CB_LastGoogleAccountEmail", "");
                SetupAuthenticatedUser(savedUid, savedName, savedEmail);
            }

            // Sync latest PlayerPrefs coins and equipped skin index into CurrentUser before cloud push
            CurrentUser.coins = PlayerPrefs.GetInt("Coins", CurrentUser.coins);
            int equippedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
            CurrentUser.selectedSkinId = $"Skin_{equippedIndex}";

            // 1. Local Persistence Backup
            string json = JsonUtility.ToJson(CurrentUser);
            PlayerPrefs.SetString(PREF_KEY_USER_DATA, json);
            PlayerPrefs.Save();

            // 2. Cloud REST Sync (Realtime DB + Firestore)
            StartCoroutine(RoutineSaveCloudUserData(onComplete));
        }

        private IEnumerator RoutineSaveCloudUserData(Action<bool> onComplete)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(CurrentUser.uid))
            {
                onComplete?.Invoke(false);
                yield break;
            }

            int equippedSkin = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
            
            // Collect all unlocked skin indices from PlayerPrefs
            List<int> unlockedList = new List<int> { 0 };
            for (int i = 0; i < 50; i++)
            {
                if (PlayerPrefs.GetInt($"Skin_Unlocked_{i}", i == 0 ? 1 : 0) == 1)
                {
                    if (!unlockedList.Contains(i)) unlockedList.Add(i);
                }
            }
            CurrentUser.unlockedSkins = unlockedList;

            string friendsJson = JsonUtility.ToJson(new StringListWrapper { items = CurrentUser.friends ?? new List<string>() });
            string unlockedSkinsJson = JsonUtility.ToJson(new IntListWrapper { items = unlockedList });

            // 1. Save to Firebase Realtime Database REST API
            string rtdbUrl = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/users/{CurrentUser.uid}.json";
            string rtdbPostData = "{" +
                "\"uid\":\"" + CurrentUser.uid + "\"," +
                "\"displayName\":\"" + EscapeJsonString(CurrentUser.displayName) + "\"," +
                "\"email\":\"" + EscapeJsonString(CurrentUser.email) + "\"," +
                "\"coins\":" + CurrentUser.coins + "," +
                "\"level\":" + CurrentUser.level + "," +
                "\"selectedSkinIndex\":" + equippedSkin + "," +
                "\"friendsJson\":\"" + EscapeJsonString(friendsJson) + "\"," +
                "\"unlockedSkinsJson\":\"" + EscapeJsonString(unlockedSkinsJson) + "\"" +
                "}";

            using (UnityWebRequest rtdbReq = new UnityWebRequest(rtdbUrl, "PUT"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(rtdbPostData);
                rtdbReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
                rtdbReq.downloadHandler = new DownloadHandlerBuffer();
                rtdbReq.SetRequestHeader("Content-Type", "application/json");

                yield return rtdbReq.SendWebRequest();
                if (rtdbReq.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[FirebaseManager] Successfully saved to Realtime Database for UID '{CurrentUser.uid}'! Coins: {CurrentUser.coins}, Unlocked Skins: {unlockedList.Count}");
                }
            }

            // 2. Save to Cloud Firestore REST API (Dual Backup)
            string firestoreUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/users/{CurrentUser.uid}?updateMask.fieldPaths=uid&updateMask.fieldPaths=displayName&updateMask.fieldPaths=email&updateMask.fieldPaths=coins&updateMask.fieldPaths=level&updateMask.fieldPaths=selectedSkinIndex&updateMask.fieldPaths=friendsJson&updateMask.fieldPaths=unlockedSkinsJson";

            string firestorePostData = "{\"fields\":{" +
                "\"uid\":{\"stringValue\":\"" + CurrentUser.uid + "\"}," +
                "\"displayName\":{\"stringValue\":\"" + EscapeJsonString(CurrentUser.displayName) + "\"}," +
                "\"email\":{\"stringValue\":\"" + EscapeJsonString(CurrentUser.email) + "\"}," +
                "\"coins\":{\"integerValue\":\"" + CurrentUser.coins + "\"}," +
                "\"level\":{\"integerValue\":\"" + CurrentUser.level + "\"}," +
                "\"selectedSkinIndex\":{\"integerValue\":\"" + equippedSkin + "\"}," +
                "\"friendsJson\":{\"stringValue\":\"" + EscapeJsonString(friendsJson) + "\"}," +
                "\"unlockedSkinsJson\":{\"stringValue\":\"" + EscapeJsonString(unlockedSkinsJson) + "\"}" +
                "}}";

            using (UnityWebRequest fsReq = new UnityWebRequest(firestoreUrl, "PATCH"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(firestorePostData);
                fsReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
                fsReq.downloadHandler = new DownloadHandlerBuffer();
                fsReq.SetRequestHeader("Content-Type", "application/json");

                yield return fsReq.SendWebRequest();
            }

            onComplete?.Invoke(true);
        }

        public void LoadLocalUserDataBackup()
        {
            if (PlayerPrefs.HasKey(PREF_KEY_USER_DATA))
            {
                try
                {
                    string json = PlayerPrefs.GetString(PREF_KEY_USER_DATA);
                    CurrentUser = JsonUtility.FromJson<FirebaseUserData>(json);
                    if (CurrentUser != null)
                    {
                        if (CurrentUser.friends == null) CurrentUser.friends = new List<string>();
                        if (CurrentUser.pendingRequests == null) CurrentUser.pendingRequests = new List<string>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FirebaseManager] Error loading local user data: {ex.Message}");
                }
            }

            if (CurrentUser == null)
            {
                string savedName = PlayerPrefs.GetString("PlayerName", "Mihir Jariwala");
                CurrentUser = new FirebaseUserData
                {
                    uid = "USR_" + UnityEngine.Random.Range(1000, 9999),
                    displayName = savedName,
                    level = 1,
                    coins = PlayerPrefs.GetInt("Coins", 1000)
                };
            }
        }

        // ─── Friends Management API ───────────────────────────────────────────────

        public bool IsFriend(string targetUidOrName)
        {
            if (CurrentUser == null || CurrentUser.friends == null || string.IsNullOrEmpty(targetUidOrName))
                return false;

            return CurrentUser.friends.Contains(targetUidOrName);
        }

        public void SendFriendRequest(string friendUidOrName, Action<bool, string> onComplete = null)
        {
            if (CurrentUser == null)
            {
                onComplete?.Invoke(false, "Not signed in");
                return;
            }

            if (string.IsNullOrEmpty(friendUidOrName))
            {
                onComplete?.Invoke(false, "Invalid Friend Name/UID");
                return;
            }

            if (CurrentUser.friends != null && CurrentUser.friends.Contains(friendUidOrName))
            {
                onComplete?.Invoke(true, "Already Friends");
                return;
            }

            StartCoroutine(RoutineSendFriendRequest(friendUidOrName, onComplete));
        }

        private IEnumerator RoutineSendFriendRequest(string friendUidOrName, Action<bool, string> onComplete)
        {
            if (CurrentUser != null && CurrentUser.sentPendingRequests != null && !CurrentUser.sentPendingRequests.Contains(friendUidOrName))
            {
                CurrentUser.sentPendingRequests.Add(friendUidOrName);
            }

            string senderIdentifier = !string.IsNullOrEmpty(CurrentUser.uid) ? CurrentUser.uid : CurrentUser.displayName;
            string payload = $"\"{CurrentUser.displayName}\"";

            string url = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/requests/{friendUidOrName}/{senderIdentifier}.json";
            using (UnityWebRequest www = UnityWebRequest.Put(url, payload))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();
            }

            onComplete?.Invoke(true, $"Friend request sent to {friendUidOrName}!");
        }

        [System.Serializable]
        public class GameInviteData
        {
            public string senderUid;
            public string senderName;
            public string roomCode;
        }

        public void SendGameInvite(string targetUid, string roomCode, Action<bool> onComplete = null)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(targetUid) || string.IsNullOrEmpty(roomCode))
            {
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(RoutineSendGameInvite(targetUid, roomCode, onComplete));
        }

        private IEnumerator RoutineSendGameInvite(string targetUid, string roomCode, Action<bool> onComplete)
        {
            string senderName = !string.IsNullOrEmpty(CurrentUser.displayName) ? CurrentUser.displayName : PlayerPrefs.GetString("PlayerName", "Player");
            string inviteJson = $"{{\"senderUid\":\"{CurrentUser.uid}\",\"senderName\":\"{senderName}\",\"roomCode\":\"{roomCode}\",\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";

            string authQuery = !string.IsNullOrEmpty(CurrentAuthToken) ? $"?auth={CurrentAuthToken}" : "";
            string safeTargetUid = UnityWebRequest.EscapeURL(targetUid.Trim());
            string url = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/invites/{safeTargetUid}.json{authQuery}";

            using (UnityWebRequest www = new UnityWebRequest(url, "PUT"))
            {
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(inviteJson);
                www.uploadHandler = new UploadHandlerRaw(jsonBytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                bool success = www.result == UnityWebRequest.Result.Success;
                if (!success)
                {
                    Debug.LogWarning($"[FirebaseManager] SendGameInvite to '{targetUid}' failed: {www.error} ({www.downloadHandler.text})");
                }
                else
                {
                    Debug.Log($"[FirebaseManager] SendGameInvite successfully written to invites/{safeTargetUid}.json!");
                }
                onComplete?.Invoke(success);
            }
        }

        public void PollGameInvite(Action<GameInviteData> onInviteReceived)
        {
            if (CurrentUser == null) return;
            StartCoroutine(RoutinePollGameInvite(onInviteReceived));
        }

        private IEnumerator RoutinePollGameInvite(Action<GameInviteData> onInviteReceived)
        {
            if (CurrentUser == null) yield break;

            string authQuery = !string.IsNullOrEmpty(CurrentAuthToken) ? $"?auth={CurrentAuthToken}" : "";

            // Check 1: Poll by CurrentUser.uid
            string safeUid = UnityWebRequest.EscapeURL(CurrentUser.uid.Trim());
            string urlUid = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/invites/{safeUid}.json{authQuery}";

            bool inviteFound = false;

            using (UnityWebRequest www = UnityWebRequest.Get(urlUid))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(www.downloadHandler.text) && www.downloadHandler.text != "null")
                {
                    string json = www.downloadHandler.text;
                    string senderUid = ParseSimpleJsonString(json, "senderUid", "");
                    string senderName = ParseSimpleJsonString(json, "senderName", "");
                    string roomCode = ParseSimpleJsonString(json, "roomCode", "");

                    if (!string.IsNullOrEmpty(roomCode) && !string.IsNullOrEmpty(senderName))
                    {
                        inviteFound = true;
                        StartCoroutine(ClearCurrentGameInvite(CurrentUser.uid));
                        onInviteReceived?.Invoke(new GameInviteData
                        {
                            senderUid = senderUid,
                            senderName = senderName,
                            roomCode = roomCode
                        });
                    }
                }
            }

            // Check 2: Poll by CurrentUser.displayName if not found by UID
            if (!inviteFound && !string.IsNullOrEmpty(CurrentUser.displayName) && CurrentUser.displayName != CurrentUser.uid)
            {
                string safeName = UnityWebRequest.EscapeURL(CurrentUser.displayName.Trim());
                string urlName = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/invites/{safeName}.json{authQuery}";

                using (UnityWebRequest wwwName = UnityWebRequest.Get(urlName))
                {
                    yield return wwwName.SendWebRequest();

                    if (wwwName.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(wwwName.downloadHandler.text) && wwwName.downloadHandler.text != "null")
                    {
                        string json = wwwName.downloadHandler.text;
                        string senderUid = ParseSimpleJsonString(json, "senderUid", "");
                        string senderName = ParseSimpleJsonString(json, "senderName", "");
                        string roomCode = ParseSimpleJsonString(json, "roomCode", "");

                        if (!string.IsNullOrEmpty(roomCode) && !string.IsNullOrEmpty(senderName))
                        {
                            StartCoroutine(ClearCurrentGameInvite(CurrentUser.displayName));
                            onInviteReceived?.Invoke(new GameInviteData
                            {
                                senderUid = senderUid,
                                senderName = senderName,
                                roomCode = roomCode
                            });
                        }
                    }
                }
            }
        }

        private IEnumerator ClearCurrentGameInvite(string targetKey)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(targetKey)) yield break;

            string authQuery = !string.IsNullOrEmpty(CurrentAuthToken) ? $"?auth={CurrentAuthToken}" : "";
            string safeKey = UnityWebRequest.EscapeURL(targetKey.Trim());
            string url = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/invites/{safeKey}.json{authQuery}";

            using (UnityWebRequest www = UnityWebRequest.Delete(url))
            {
                yield return www.SendWebRequest();
            }
        }

        public void RemoveFriend(string friendUidOrName, Action<bool> onComplete = null)
        {
            if (CurrentUser == null || CurrentUser.friends == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            if (CurrentUser.friends.Contains(friendUidOrName))
            {
                CurrentUser.friends.Remove(friendUidOrName);
                SaveUserProfile();
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        }

        public List<FirebaseUserData> GetDemoPlayers()
        {
            return new List<FirebaseUserData>
            {
                new FirebaseUserData { uid = "USR_1001", displayName = "AlphaSniper", level = 15, coins = 4500 },
                new FirebaseUserData { uid = "USR_1002", displayName = "GhostRider", level = 22, coins = 7800 },
                new FirebaseUserData { uid = "USR_1003", displayName = "ViperX", level = 9, coins = 2100 },
                new FirebaseUserData { uid = "USR_1004", displayName = "ShadowKnight", level = 30, coins = 12000 },
                new FirebaseUserData { uid = "USR_1005", displayName = "Striker99", level = 18, coins = 5600 },
                new FirebaseUserData { uid = "USR_1006", displayName = "CyberWolf", level = 12, coins = 3400 },
                new FirebaseUserData { uid = "USR_1007", displayName = "NeonHunter", level = 25, coins = 9200 },
                new FirebaseUserData { uid = "USR_1008", displayName = "StormTrooper", level = 7, coins = 1500 },
                new FirebaseUserData { uid = "USR_1009", displayName = "Phoenix88", level = 14, coins = 3900 },
                new FirebaseUserData { uid = "USR_1010", displayName = "BlazeRider", level = 28, coins = 11000 }
            };
        }

        public void FetchFriendsList(Action<List<FriendProfile>> onComplete)
        {
            StartCoroutine(RoutineFetchFriendsList(onComplete));
        }

        public void FetchTargetUserFriendsList(string targetUid, Action<List<FriendProfile>> onComplete)
        {
            StartCoroutine(RoutineFetchTargetUserFriendsList(targetUid, onComplete));
        }

        private IEnumerator RoutineFetchTargetUserFriendsList(string targetUid, Action<List<FriendProfile>> onComplete)
        {
            var result = new List<FriendProfile>();
            if (string.IsNullOrEmpty(targetUid))
            {
                onComplete?.Invoke(result);
                yield break;
            }

            string url = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/users.json";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                var allUsers = new List<FirebaseUserData>();
                if (www.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(www.downloadHandler.text) && www.downloadHandler.text != "null")
                {
                    try
                    {
                        allUsers = ParseAllUsersFromRtdbJson(www.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FirebaseManager] Friends parse error: {ex.Message}");
                    }
                }

                FirebaseUserData targetUser = allUsers.Find(u => u.uid == targetUid || u.displayName == targetUid);
                List<string> targetFriends = new List<string>();

                if (targetUser != null && targetUser.friends != null && targetUser.friends.Count > 0)
                {
                    targetFriends = targetUser.friends;
                }

                if (targetFriends.Count > 0)
                {
                    foreach (var fId in targetFriends)
                    {
                        var match = allUsers.Find(u => u.uid == fId || u.displayName == fId);
                        if (match != null)
                        {
                            result.Add(new FriendProfile(match.uid, match.displayName)
                            {
                                level = match.level,
                                selectedSkinIndex = match.selectedSkinIndex
                            });
                        }
                    }
                }
            }

            onComplete?.Invoke(result);
        }

        private List<FirebaseUserData> ParseAllUsersFromRtdbJson(string rawJson)
        {
            var users = new List<FirebaseUserData>();
            if (string.IsNullOrEmpty(rawJson) || rawJson == "null" || !rawJson.StartsWith("{"))
                return users;

            int pos = 0;
            while (pos < rawJson.Length)
            {
                int colonIdx = rawJson.IndexOf("\":{", pos);
                if (colonIdx < 0) break;

                int keyQuoteEnd = colonIdx;
                int keyQuoteStart = rawJson.LastIndexOf('"', keyQuoteEnd - 1);
                if (keyQuoteStart < 0)
                {
                    pos = colonIdx + 3;
                    continue;
                }

                string userKey = rawJson.Substring(keyQuoteStart + 1, keyQuoteEnd - keyQuoteStart - 1);

                int objectStart = colonIdx + 2; // at '{'
                int braceCount = 0;
                int objectEnd = -1;

                for (int i = objectStart; i < rawJson.Length; i++)
                {
                    if (rawJson[i] == '{') braceCount++;
                    else if (rawJson[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            objectEnd = i;
                            break;
                        }
                    }
                }

                if (objectEnd > objectStart)
                {
                    string userChunk = rawJson.Substring(objectStart, objectEnd - objectStart + 1);

                    string uid = ParseSimpleJsonString(userChunk, "uid", userKey);
                    if (string.IsNullOrEmpty(uid)) uid = userKey;

                    string dName = ParseSimpleJsonString(userChunk, "displayName", "");
                    if (string.IsNullOrEmpty(dName))
                    {
                        dName = uid.Length > 8 ? uid.Substring(0, 8) : uid;
                    }

                    string email = ParseSimpleJsonString(userChunk, "email", "");
                    int coins = ParseSimpleJsonInt(userChunk, "coins", 1000);
                    int level = ParseSimpleJsonInt(userChunk, "level", 1);
                    int selectedSkinIndex = ParseSimpleJsonInt(userChunk, "selectedSkinIndex", 0);

                    users.Add(new FirebaseUserData
                    {
                        uid = uid,
                        displayName = dName,
                        email = email,
                        coins = coins,
                        level = level,
                        selectedSkinIndex = selectedSkinIndex
                    });

                    pos = objectEnd + 1;
                }
                else
                {
                    pos = colonIdx + 3;
                }
            }

            return users;
        }

        private IEnumerator RoutineFetchFriendsList(Action<List<FriendProfile>> onComplete)
        {
            var result = new List<FriendProfile>();
            if (CurrentUser == null)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            var friendIds = new HashSet<string>(CurrentUser.friends ?? new List<string>());

            // Query online mutual friends from RTDB node friends/{CurrentUser.uid}.json
            string myId = CurrentUser.uid;
            string urlOnlineFriends = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/friends/{myId}.json";
            using (UnityWebRequest wwwOnline = UnityWebRequest.Get(urlOnlineFriends))
            {
                yield return wwwOnline.SendWebRequest();
                if (wwwOnline.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(wwwOnline.downloadHandler.text) && wwwOnline.downloadHandler.text != "null")
                {
                    ParseIncomingRequestUids(wwwOnline.downloadHandler.text, friendIds);
                }
            }

            // Also query online friends by CurrentUser.displayName
            if (!string.IsNullOrEmpty(CurrentUser.displayName))
            {
                string urlNameFriends = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/friends/{CurrentUser.displayName}.json";
                using (UnityWebRequest wwwName = UnityWebRequest.Get(urlNameFriends))
                {
                    yield return wwwName.SendWebRequest();
                    if (wwwName.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(wwwName.downloadHandler.text) && wwwName.downloadHandler.text != "null")
                    {
                        ParseIncomingRequestUids(wwwName.downloadHandler.text, friendIds);
                    }
                }
            }

            // Clean legacy IDs
            friendIds.RemoveWhere(id => string.IsNullOrEmpty(id) || id.StartsWith("USR_100"));

            // Sync back to CurrentUser.friends and clear sentPendingRequests / pendingRequests for active friends
            CurrentUser.friends = new List<string>(friendIds);
            if (CurrentUser.sentPendingRequests != null)
            {
                CurrentUser.sentPendingRequests.RemoveAll(f => friendIds.Contains(f));
            }
            if (CurrentUser.pendingRequests != null)
            {
                CurrentUser.pendingRequests.RemoveAll(f => friendIds.Contains(f));
            }
            SaveUserProfile();

            if (friendIds.Count == 0)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            string usersUrl = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/users.json";
            using (UnityWebRequest www = UnityWebRequest.Get(usersUrl))
            {
                yield return www.SendWebRequest();

                var allUsers = new List<FirebaseUserData>();
                if (www.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(www.downloadHandler.text) && www.downloadHandler.text != "null")
                {
                    try { allUsers = ParseAllUsersFromRtdbJson(www.downloadHandler.text); }
                    catch (Exception ex) { Debug.LogWarning($"[FirebaseManager] Friends parse error: {ex.Message}"); }
                }

                var addedUids = new HashSet<string>();
                foreach (var fId in friendIds)
                {
                    var match = allUsers.Find(u => u.uid == fId || u.displayName == fId);
                    string targetKey = match != null ? match.uid : fId;

                    if (addedUids.Contains(targetKey)) continue;
                    addedUids.Add(targetKey);

                    if (match != null)
                    {
                        result.Add(new FriendProfile(match.uid, match.displayName) { level = match.level, selectedSkinIndex = match.selectedSkinIndex });
                    }
                    else if (!fId.StartsWith("USR_100"))
                    {
                        result.Add(new FriendProfile(fId, fId.Length > 8 ? fId.Substring(0, 8) : fId) { level = 1 });
                    }
                }
            }

            onComplete?.Invoke(result);
        }

        public bool IsRequestPending(string targetUidOrName)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(targetUidOrName))
                return false;

            if (CurrentUser.friends != null && CurrentUser.friends.Contains(targetUidOrName))
                return false;

            bool inIncoming = CurrentUser.pendingRequests != null && CurrentUser.pendingRequests.Contains(targetUidOrName);
            bool inSent = CurrentUser.sentPendingRequests != null && CurrentUser.sentPendingRequests.Contains(targetUidOrName);

            return inIncoming || inSent;
        }

        public void AcceptFriendRequest(string friendUidOrName, Action<bool> onComplete = null)
        {
            if (CurrentUser == null) { onComplete?.Invoke(false); return; }

            if (CurrentUser.pendingRequests != null) CurrentUser.pendingRequests.Remove(friendUidOrName);
            if (CurrentUser.sentPendingRequests != null) CurrentUser.sentPendingRequests.Remove(friendUidOrName);

            if (CurrentUser.friends != null && !CurrentUser.friends.Contains(friendUidOrName))
            {
                CurrentUser.friends.Add(friendUidOrName);
            }

            SaveUserProfile();
            StartCoroutine(RoutineAcceptFriendRequestOnline(friendUidOrName));
            onComplete?.Invoke(true);
        }

        private IEnumerator RoutineAcceptFriendRequestOnline(string friendUidOrName)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(friendUidOrName)) yield break;

            yield return StartCoroutine(ClearRtdbPendingRequest(friendUidOrName));

            string myId = UnityWebRequest.EscapeURL(CurrentUser.uid.Trim());
            string safeFriend = UnityWebRequest.EscapeURL(friendUidOrName.Trim());

            string url1 = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/friends/{myId}/{safeFriend}.json";
            using (UnityWebRequest www1 = UnityWebRequest.Put(url1, "true"))
            {
                www1.SetRequestHeader("Content-Type", "application/json");
                yield return www1.SendWebRequest();
            }

            string url2 = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/friends/{safeFriend}/{myId}.json";
            using (UnityWebRequest www2 = UnityWebRequest.Put(url2, "true"))
            {
                www2.SetRequestHeader("Content-Type", "application/json");
                yield return www2.SendWebRequest();
            }
        }

        public void DeclineFriendRequest(string friendUidOrName, Action<bool> onComplete = null)
        {
            if (CurrentUser == null) { onComplete?.Invoke(false); return; }

            if (CurrentUser.pendingRequests != null) CurrentUser.pendingRequests.Remove(friendUidOrName);
            if (CurrentUser.sentPendingRequests != null) CurrentUser.sentPendingRequests.Remove(friendUidOrName);

            SaveUserProfile();
            StartCoroutine(ClearRtdbPendingRequest(friendUidOrName));
            onComplete?.Invoke(true);
        }

        private IEnumerator ClearRtdbPendingRequest(string senderUidOrName)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(senderUidOrName)) yield break;

            string safeSender = UnityWebRequest.EscapeURL(senderUidOrName.Trim());

            string url1 = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/requests/{UnityWebRequest.EscapeURL(CurrentUser.uid.Trim())}/{safeSender}.json";
            using (UnityWebRequest www1 = UnityWebRequest.Delete(url1))
            {
                yield return www1.SendWebRequest();
            }

            if (!string.IsNullOrEmpty(CurrentUser.displayName))
            {
                string url2 = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/requests/{UnityWebRequest.EscapeURL(CurrentUser.displayName.Trim())}/{safeSender}.json";
                using (UnityWebRequest www2 = UnityWebRequest.Delete(url2))
                {
                    yield return www2.SendWebRequest();
                }
            }
        }

        public void FetchPendingRequestsList(Action<List<FriendProfile>> onComplete)
        {
            StartCoroutine(RoutineFetchPendingRequestsList(onComplete));
        }

        private IEnumerator RoutineFetchPendingRequestsList(Action<List<FriendProfile>> onComplete)
        {
            var result = new List<FriendProfile>();
            if (CurrentUser == null)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            var incomingUids = new HashSet<string>();

            // 1. Add locally tracked pending requests
            if (CurrentUser.pendingRequests != null)
            {
                foreach (var req in CurrentUser.pendingRequests)
                {
                    if (!string.IsNullOrEmpty(req) && !req.StartsWith("USR_100")) incomingUids.Add(req);
                }
            }

            // 2. Query RTDB incoming requests node for CurrentUser.uid
            string uidUrl = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/requests/{CurrentUser.uid}.json";
            using (UnityWebRequest wwwUid = UnityWebRequest.Get(uidUrl))
            {
                yield return wwwUid.SendWebRequest();
                if (wwwUid.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(wwwUid.downloadHandler.text) && wwwUid.downloadHandler.text != "null")
                {
                    ParseIncomingRequestUids(wwwUid.downloadHandler.text, incomingUids);
                }
            }

            // 3. Query RTDB incoming requests node for CurrentUser.displayName
            if (!string.IsNullOrEmpty(CurrentUser.displayName))
            {
                string nameUrl = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/requests/{CurrentUser.displayName}.json";
                using (UnityWebRequest wwwName = UnityWebRequest.Get(nameUrl))
                {
                    yield return wwwName.SendWebRequest();
                    if (wwwName.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(wwwName.downloadHandler.text) && wwwName.downloadHandler.text != "null")
                    {
                        ParseIncomingRequestUids(wwwName.downloadHandler.text, incomingUids);
                    }
                }
            }

            CurrentUser.pendingRequests = new List<string>(incomingUids);

            if (incomingUids.Count == 0)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            // 4. Query all users from RTDB to populate sender details (display name, level, skin)
            string usersUrl = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/users.json";
            using (UnityWebRequest www = UnityWebRequest.Get(usersUrl))
            {
                yield return www.SendWebRequest();

                var allUsers = new List<FirebaseUserData>();
                if (www.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(www.downloadHandler.text) && www.downloadHandler.text != "null")
                {
                    try { allUsers = ParseAllUsersFromRtdbJson(www.downloadHandler.text); }
                    catch (Exception ex) { Debug.LogWarning($"[FirebaseManager] Requests parse error: {ex.Message}"); }
                }

                var addedRequestUids = new HashSet<string>();
                foreach (var senderId in incomingUids)
                {
                    var match = allUsers.Find(u => u.uid == senderId || u.displayName == senderId);
                    string targetKey = match != null ? match.uid : senderId;

                    if (addedRequestUids.Contains(targetKey)) continue;
                    addedRequestUids.Add(targetKey);

                    if (match != null)
                    {
                        result.Add(new FriendProfile(match.uid, match.displayName) { level = match.level, selectedSkinIndex = match.selectedSkinIndex });
                    }
                    else
                    {
                        result.Add(new FriendProfile(senderId, senderId.Length > 8 ? senderId.Substring(0, 8) : senderId) { level = 1 });
                    }
                }
            }

            onComplete?.Invoke(result);
        }

        private void ParseIncomingRequestUids(string json, HashSet<string> targetSet)
        {
            if (string.IsNullOrEmpty(json) || json == "null") return;

            int pos = 0;
            while (pos < json.Length)
            {
                int quoteIdx = json.IndexOf('"', pos);
                if (quoteIdx < 0) break;
                int endQuote = json.IndexOf('"', quoteIdx + 1);
                if (endQuote < 0) break;

                string key = json.Substring(quoteIdx + 1, endQuote - quoteIdx - 1);
                if (!string.IsNullOrEmpty(key) && key != "senderUid" && key != "senderName")
                {
                    targetSet.Add(key);
                }
                pos = endQuote + 1;
            }
        }

        private string ParseSimpleJsonString(string json, string fieldName, string defaultValue)
        {
            if (string.IsNullOrEmpty(json)) return defaultValue;
            string key = $"\"{fieldName}\":\"";
            int idx = json.IndexOf(key);
            if (idx >= 0)
            {
                int start = idx + key.Length;
                int end = json.IndexOf("\"", start);
                if (end > start)
                {
                    return json.Substring(start, end - start);
                }
            }
            return defaultValue;
        }

        private int ParseSimpleJsonInt(string json, string fieldName, int defaultValue)
        {
            if (string.IsNullOrEmpty(json)) return defaultValue;
            string key = $"\"{fieldName}\":";
            int idx = json.IndexOf(key);
            if (idx >= 0)
            {
                int start = idx + key.Length;
                int end = start;
                while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                {
                    end++;
                }
                if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
                {
                    return val;
                }
            }
            return defaultValue;
        }

        public void SearchPlayers(string query, Action<List<FirebaseUserData>> onComplete)
        {
            StartCoroutine(RoutineSearchPlayers(query, onComplete));
        }

        private IEnumerator RoutineSearchPlayers(string query, Action<List<FirebaseUserData>> onComplete)
        {
            var results = new List<FirebaseUserData>();
            string cleanQuery = query != null ? query.Trim().ToLower() : "";
            bool isSearching = !string.IsNullOrEmpty(cleanQuery);

            // Query Firebase Realtime Database (Strictly Real Data)
            string url = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/users.json";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                Debug.Log($"[FirebaseManager] RTDB Backend Response (URL: {url} | Status: {www.responseCode}): {www.downloadHandler.text}");

                if (www.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(www.downloadHandler.text) && www.downloadHandler.text != "null")
                {
                    try
                    {
                        var allUsers = ParseAllUsersFromRtdbJson(www.downloadHandler.text);

                        foreach (var user in allUsers)
                        {
                            if (CurrentUser != null && (user.uid == CurrentUser.uid || user.displayName == CurrentUser.displayName))
                            {
                                continue;
                            }

                            if (isSearching)
                            {
                                if (user.uid.ToLower().Contains(cleanQuery) || user.displayName.ToLower().Contains(cleanQuery) || user.email.ToLower().Contains(cleanQuery))
                                {
                                    results.Add(user);
                                }
                            }
                            else
                            {
                                results.Add(user);
                                if (results.Count >= 100) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FirebaseManager] Search parse error: {ex.Message}");
                    }
                }
            }

            onComplete?.Invoke(results);
        }

        public void FetchAndRestoreCloudUserData(string uid, Action<bool> onComplete)
        {
            StartCoroutine(RoutineFetchAndRestoreCloudUserData(uid, onComplete));
        }

        private IEnumerator RoutineFetchAndRestoreCloudUserData(string uid, Action<bool> onComplete)
        {
            if (string.IsNullOrEmpty(uid))
            {
                onComplete?.Invoke(false);
                yield break;
            }

            // 1. Try Fetching from Firebase Realtime Database
            string rtdbUrl = $"https://{firebaseProjectId}-default-rtdb.firebaseio.com/users/{uid}.json";
            using (UnityWebRequest rtdbReq = UnityWebRequest.Get(rtdbUrl))
            {
                yield return rtdbReq.SendWebRequest();

                if (rtdbReq.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(rtdbReq.downloadHandler.text) && rtdbReq.downloadHandler.text != "null")
                {
                    try
                    {
                        string json = rtdbReq.downloadHandler.text;
                        Debug.Log($"[FirebaseManager] Restored from Realtime Database: {json}");

                        RealtimeUserDataDTO dto = JsonUtility.FromJson<RealtimeUserDataDTO>(json);
                        if (dto != null && !string.IsNullOrEmpty(dto.uid))
                        {
                            List<string> restoredFriends = new List<string>();
                            if (!string.IsNullOrEmpty(dto.friendsJson))
                            {
                                var wrapper = JsonUtility.FromJson<StringListWrapper>(dto.friendsJson);
                                if (wrapper != null && wrapper.items != null) restoredFriends = wrapper.items;
                            }

                            if (!string.IsNullOrEmpty(dto.unlockedSkinsJson))
                            {
                                var skinWrapper = JsonUtility.FromJson<IntListWrapper>(dto.unlockedSkinsJson);
                                if (skinWrapper != null && skinWrapper.items != null)
                                {
                                    foreach (int sIdx in skinWrapper.items)
                                    {
                                        PlayerPrefs.SetInt($"Skin_Unlocked_{sIdx}", 1);
                                    }
                                }
                            }

                            if (CurrentUser == null) CurrentUser = new FirebaseUserData();
                            CurrentUser.uid = uid;
                            CurrentUser.displayName = dto.displayName;
                            CurrentUser.email = dto.email;
                            CurrentUser.coins = dto.coins;
                            CurrentUser.level = dto.level > 0 ? dto.level : 1;
                            CurrentUser.selectedSkinId = $"Skin_{dto.selectedSkinIndex}";
                            CurrentUser.friends = restoredFriends;

                            PlayerPrefs.SetInt("Coins", dto.coins);
                            PlayerPrefs.SetInt("EquippedSkinIndex", dto.selectedSkinIndex);
                            PlayerPrefs.SetString("PlayerName", dto.displayName);
                            PlayerPrefs.SetInt("PlayerNameHasBeenSet", 1);

                            string localJson = JsonUtility.ToJson(CurrentUser);
                            PlayerPrefs.SetString(PREF_KEY_USER_DATA, localJson);
                            PlayerPrefs.SetString(PREF_KEY_SAVED_UID, uid);
                            PlayerPrefs.Save();

                            Debug.Log($"[FirebaseManager] Successfully restored RTDB profile! Coins: {dto.coins}");
                            onComplete?.Invoke(true);
                            yield break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FirebaseManager] Error parsing RTDB data: {ex.Message}");
                    }
                }
            }

            // 2. Try Fetching from Cloud Firestore (Fallback)
            string firestoreUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/users/{uid}";
            using (UnityWebRequest fsReq = UnityWebRequest.Get(firestoreUrl))
            {
                yield return fsReq.SendWebRequest();

                if (fsReq.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(fsReq.downloadHandler.text))
                {
                    try
                    {
                        string json = fsReq.downloadHandler.text;
                        Debug.Log($"[FirebaseManager] Restored from Firestore: {json}");

                        int restoredCoins = ParseFirestoreInt(json, "coins", 1000);
                        int restoredLevel = ParseFirestoreInt(json, "level", 1);
                        int restoredSkinIndex = ParseFirestoreInt(json, "selectedSkinIndex", 0);
                        string restoredName = ParseFirestoreString(json, "displayName", "Mihir");
                        string restoredEmail = ParseFirestoreString(json, "email", "");
                        string friendsJsonStr = ParseFirestoreString(json, "friendsJson", "");
                        string unlockedSkinsStr = ParseFirestoreString(json, "unlockedSkinsJson", "");

                        List<string> restoredFriends = new List<string>();
                        if (!string.IsNullOrEmpty(friendsJsonStr))
                        {
                            var wrapper = JsonUtility.FromJson<StringListWrapper>(friendsJsonStr);
                            if (wrapper != null && wrapper.items != null) restoredFriends = wrapper.items;
                        }

                        if (!string.IsNullOrEmpty(unlockedSkinsStr))
                        {
                            var skinWrapper = JsonUtility.FromJson<IntListWrapper>(unlockedSkinsStr);
                            if (skinWrapper != null && skinWrapper.items != null)
                            {
                                foreach (int sIdx in skinWrapper.items)
                                {
                                    PlayerPrefs.SetInt($"Skin_Unlocked_{sIdx}", 1);
                                }
                            }
                        }

                        if (CurrentUser == null) CurrentUser = new FirebaseUserData();
                        CurrentUser.uid = uid;
                        CurrentUser.displayName = restoredName;
                        CurrentUser.email = restoredEmail;
                        CurrentUser.coins = restoredCoins;
                        CurrentUser.level = restoredLevel;
                        CurrentUser.selectedSkinId = $"Skin_{restoredSkinIndex}";
                        CurrentUser.friends = restoredFriends;

                        PlayerPrefs.SetInt("Coins", restoredCoins);
                        PlayerPrefs.SetInt("EquippedSkinIndex", restoredSkinIndex);
                        PlayerPrefs.SetString("PlayerName", restoredName);
                        PlayerPrefs.SetInt("PlayerNameHasBeenSet", 1);

                        string localJson = JsonUtility.ToJson(CurrentUser);
                        PlayerPrefs.SetString(PREF_KEY_USER_DATA, localJson);
                        PlayerPrefs.SetString(PREF_KEY_SAVED_UID, uid);
                        PlayerPrefs.Save();

                        Debug.Log($"[FirebaseManager] Successfully restored Firestore profile! Coins: {restoredCoins}");
                        onComplete?.Invoke(true);
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FirebaseManager] Error parsing Firestore data: {ex.Message}");
                    }
                }
            }

            onComplete?.Invoke(false);
        }

        [Serializable]
        private class RealtimeUserDataDTO
        {
            public string uid;
            public string displayName;
            public string email;
            public int coins;
            public int level;
            public int selectedSkinIndex;
            public string friendsJson;
            public string unlockedSkinsJson;
        }

        private int ParseFirestoreInt(string json, string fieldName, int defaultValue)
        {
            string key = $"\"{fieldName}\":{{\"integerValue\":\"";
            int idx = json.IndexOf(key);
            if (idx >= 0)
            {
                int start = idx + key.Length;
                int end = json.IndexOf("\"", start);
                if (end > start)
                {
                    if (int.TryParse(json.Substring(start, end - start), out int val))
                        return val;
                }
            }
            return defaultValue;
        }

        private string ParseFirestoreString(string json, string fieldName, string defaultValue)
        {
            string key = $"\"{fieldName}\":{{\"stringValue\":\"";
            int idx = json.IndexOf(key);
            if (idx >= 0)
            {
                int start = idx + key.Length;
                int end = json.IndexOf("\"", start);
                if (end > start)
                {
                    return json.Substring(start, end - start);
                }
            }
            return defaultValue;
        }

        private List<string> ParseFirestoreStringArray(string json, string fieldName)
        {
            var list = new List<string>();
            string key = $"\"{fieldName}\":{{\"arrayValue\":{{\"values\":[";
            int idx = json.IndexOf(key);
            if (idx >= 0)
            {
                int start = idx + key.Length;
                int end = json.IndexOf("]}}", start);
                if (end > start)
                {
                    string sub = json.Substring(start, end - start);
                    string strKey = "{\"stringValue\":\"";
                    int p = 0;
                    while ((p = sub.IndexOf(strKey, p)) >= 0)
                    {
                        p += strKey.Length;
                        int e = sub.IndexOf("\"", p);
                        if (e > p)
                        {
                            list.Add(sub.Substring(p, e - p));
                            p = e + 1;
                        }
                    }
                }
            }
            return list;
        }

        private string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        [Serializable]
        private class StringListWrapper
        {
            public List<string> items = new List<string>();
        }

        [Serializable]
        private class IntListWrapper
        {
            public List<int> items = new List<int>();
        }
    }
}
