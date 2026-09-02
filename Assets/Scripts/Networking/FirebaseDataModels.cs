using System;
using System.Collections.Generic;
using UnityEngine;

namespace CounterBoom.Networking
{
    [Serializable]
    public class FirebaseUserData
    {
        public string uid = "";
        public string displayName = "Player";
        public string email = "";
        public string photoUrl = "";
        public int coins = 1000;
        public int level = 1;
        public int xp = 0;
        public int selectedSkinIndex = 0;
        public string selectedSkinId = "Skin_0";
        public List<int> unlockedSkins = new List<int> { 0 };
        public List<string> friends = new List<string>();
        public List<string> pendingRequests = new List<string>();
        public List<string> sentPendingRequests = new List<string>();
        public long lastLoginTimestamp = 0;

        public FirebaseUserData()
        {
            unlockedSkins = new List<int> { 0 };
            friends = new List<string>();
            pendingRequests = new List<string>();
            sentPendingRequests = new List<string>();
        }
    }

    [Serializable]
    public class FriendProfile
    {
        public string uid = "";
        public string displayName = "Friend";
        public string photoUrl = "";
        public bool isOnline = false;
        public string currentLobbyCode = "";
        public int level = 1;
        public int selectedSkinIndex = 0;

        public FriendProfile() { }

        public FriendProfile(string uid, string name)
        {
            this.uid = uid;
            this.displayName = name;
        }
    }

    [Serializable]
    public class FirebaseAuthResponse
    {
        public string kind;
        public string localId; // Firebase UID
        public string idToken;
        public string refreshToken;
        public string expiresIn;
        public string displayName;
        public string email;
        public bool isNewUser;
    }

    [Serializable]
    public class FirebaseAuthRequest
    {
        public string token;
        public string providerId = "google.com";
        public bool returnSecureToken = true;
    }

    [Serializable]
    public class GoogleDeviceCodeResponse
    {
        public string device_code;
        public string user_code;
        public string verification_url;
        public int expires_in;
        public int interval;
    }

    [Serializable]
    public class GoogleTokenResponse
    {
        public string access_token;
        public string id_token;
        public string token_type;
        public int expires_in;
    }

    [Serializable]
    public class FirestoreDocumentWrapper
    {
        public string name;
        public FirestoreFields fields;
    }

    [Serializable]
    public class FirestoreFields
    {
        public StringValue uid;
        public StringValue displayName;
        public StringValue email;
        public StringValue photoUrl;
        public IntegerValue coins;
        public StringValue friendsJson;
    }

    [Serializable] public class StringValue { public string stringValue; }
    [Serializable] public class IntegerValue { public string integerValue; }
}
