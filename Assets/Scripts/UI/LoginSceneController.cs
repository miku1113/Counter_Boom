using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using CounterBoom.Networking;

namespace CounterBoom.UI
{
    public class LoginSceneController : MonoBehaviour
    {
        [Header("Login UI References")]
        public Button          googleLoginButton;
        public Button          guestLoginButton;
        public TextMeshProUGUI statusText;
        public GameObject      loadingOverlay;

        [Header("Target Next Scene")]
        public string targetNextScene = "MainMenu";

        public static LoginSceneController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsureUIReferences();
            WireButtonListeners();
            CheckAutoLogin();
        }

        private void EnsureUIReferences()
        {
            Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            if (googleLoginButton == null)
            {
                Button[] btns = canvas.GetComponentsInChildren<Button>(true);
                foreach (var b in btns)
                {
                    if (b == null) continue;
                    string bName = b.gameObject.name.ToLower();
                    if (bName.Contains("google") || bName.Contains("glogin"))
                    {
                        googleLoginButton = b;
                        break;
                    }
                }
            }

            if (guestLoginButton == null)
            {
                Button[] btns = canvas.GetComponentsInChildren<Button>(true);
                foreach (var b in btns)
                {
                    if (b == null) continue;
                    string bName = b.gameObject.name.ToLower();
                    if (bName.Contains("guest") || bName.Contains("anonymous") || bName.Contains("play"))
                    {
                        guestLoginButton = b;
                        break;
                    }
                }
            }

            if (statusText == null)
            {
                TextMeshProUGUI[] tmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;
                    string tName = t.gameObject.name.ToLower();
                    if (tName.Contains("status") || tName.Contains("info") || tName.Contains("message"))
                    {
                        statusText = t;
                        break;
                    }
                }
            }

            if (loadingOverlay == null)
            {
                Transform loadingTr = canvas.transform.Find("LoadingOverlay") ?? canvas.transform.Find("Loading");
                if (loadingTr != null) loadingOverlay = loadingTr.gameObject;
            }
        }

        private void WireButtonListeners()
        {
            if (googleLoginButton != null)
            {
                googleLoginButton.onClick.RemoveAllListeners();
                googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);
            }

            if (guestLoginButton != null)
            {
                guestLoginButton.onClick.RemoveAllListeners();
                guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
            }
        }

        private void CheckAutoLogin()
        {
            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsSignedIn)
            {
                UpdateStatus($"Welcome back, {FirebaseManager.Instance.CurrentUser.displayName}!");
                StartCoroutine(RoutineLoadMainMenu(1.0f));
            }
            else
            {
                UpdateStatus("Choose an option to sign in");
                if (loadingOverlay != null) loadingOverlay.SetActive(false);
            }
        }

        private GameObject googleLoginModal;

        public void OnGoogleLoginClicked()
        {
            EnsureFirebaseInstance();
            UpdateStatus("Signing in with Google...");
            if (loadingOverlay != null) loadingOverlay.SetActive(true);

            FirebaseManager.Instance.StartOfficialGoogleOAuth((success, message) =>
            {
                UpdateStatus(message);
                if (success)
                {
                    StartCoroutine(RoutineLoadMainMenu(0.5f));
                }
                else
                {
                    if (loadingOverlay != null) loadingOverlay.SetActive(false);
                }
            });
        }

        public void ShowGoogleAccountInputModal()
        {
            if (googleLoginModal != null)
            {
                googleLoginModal.SetActive(true);
                googleLoginModal.transform.SetAsLastSibling();
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 1. Dark Overlay
            googleLoginModal = new GameObject("GoogleLoginModal", typeof(RectTransform), typeof(Image));
            googleLoginModal.transform.SetParent(canvas.transform, false);

            RectTransform mRt = googleLoginModal.GetComponent<RectTransform>();
            mRt.anchorMin = Vector2.zero; mRt.anchorMax = Vector2.one; mRt.offsetMin = Vector2.zero; mRt.offsetMax = Vector2.zero;

            Image mOverlay = googleLoginModal.GetComponent<Image>();
            mOverlay.color = new Color(0f, 0f, 0f, 0.75f);

            // 2. Card Panel Container
            GameObject cardGO = new GameObject("CardPanel", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(googleLoginModal.transform, false);

            RectTransform cRt = cardGO.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f); cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.sizeDelta = new Vector2(480f, 290f);

            Image cBg = cardGO.GetComponent<Image>();
            cBg.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);

            // 3. Title
            GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(cardGO.transform, false);

            RectTransform tRt = titleGO.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f); tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -12f); tRt.sizeDelta = new Vector2(0f, 35f);

            TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
            tTmp.text = "<color=#ea4335>G</color><color=#4285f4>o</color><color=#fbbc05>o</color><color=#4285f4>g</color><color=#34a853>l</color><color=#ea4335>e</color> Account Sign In";
            tTmp.fontSize = 20; tTmp.fontStyle = FontStyles.Bold; tTmp.alignment = TextAlignmentOptions.Center;

            // 4. Name Input Field Container
            GameObject nameInputGO = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            nameInputGO.transform.SetParent(cardGO.transform, false);

            RectTransform nRt = nameInputGO.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0.5f, 0.5f); nRt.anchorMax = new Vector2(0.5f, 0.5f); nRt.pivot = new Vector2(0.5f, 0.5f);
            nRt.anchoredPosition = new Vector2(0f, 40f); nRt.sizeDelta = new Vector2(400f, 40f);

            Image nBg = nameInputGO.GetComponent<Image>();
            nBg.color = new Color(0.2f, 0.25f, 0.32f, 1f);

            TMP_InputField nameInputField = nameInputGO.GetComponent<TMP_InputField>();

            GameObject nTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            nTextGO.transform.SetParent(nameInputGO.transform, false);
            RectTransform nTxtRt = nTextGO.GetComponent<RectTransform>();
            nTxtRt.anchorMin = Vector2.zero; nTxtRt.anchorMax = Vector2.one; nTxtRt.sizeDelta = Vector2.zero;
            nTxtRt.offsetMin = new Vector2(15f, 0f); nTxtRt.offsetMax = new Vector2(-15f, 0f);

            TextMeshProUGUI nTmp = nTextGO.GetComponent<TextMeshProUGUI>();
            nTmp.fontSize = 15; nTmp.alignment = TextAlignmentOptions.MidlineLeft; nTmp.color = Color.white;

            nameInputField.textComponent = nTmp;
            string lastName = PlayerPrefs.GetString("CB_LastGoogleAccountName", "Mihir Jariwala");
            nameInputField.text = lastName;

            // 5. Email Input Field Container
            GameObject emailInputGO = new GameObject("EmailInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            emailInputGO.transform.SetParent(cardGO.transform, false);

            RectTransform eRt = emailInputGO.GetComponent<RectTransform>();
            eRt.anchorMin = new Vector2(0.5f, 0.5f); eRt.anchorMax = new Vector2(0.5f, 0.5f); eRt.pivot = new Vector2(0.5f, 0.5f);
            eRt.anchoredPosition = new Vector2(0f, -10f); eRt.sizeDelta = new Vector2(400f, 40f);

            Image eBg = emailInputGO.GetComponent<Image>();
            eBg.color = new Color(0.2f, 0.25f, 0.32f, 1f);

            TMP_InputField emailInputField = emailInputGO.GetComponent<TMP_InputField>();

            GameObject eTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            eTextGO.transform.SetParent(emailInputGO.transform, false);
            RectTransform eTxtRt = eTextGO.GetComponent<RectTransform>();
            eTxtRt.anchorMin = Vector2.zero; eTxtRt.anchorMax = Vector2.one; eTxtRt.sizeDelta = Vector2.zero;
            eTxtRt.offsetMin = new Vector2(15f, 0f); eTxtRt.offsetMax = new Vector2(-15f, 0f);

            TextMeshProUGUI eTmp = eTextGO.GetComponent<TextMeshProUGUI>();
            eTmp.fontSize = 15; eTmp.alignment = TextAlignmentOptions.MidlineLeft; eTmp.color = Color.white;

            emailInputField.textComponent = eTmp;
            string lastEmail = PlayerPrefs.GetString("CB_LastGoogleAccountEmail", "mihirjariwala334@gmail.com");
            emailInputField.text = lastEmail;

            // 6. Sign In Button
            GameObject btnGO = new GameObject("SignInBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(cardGO.transform, false);

            RectTransform bRt = btnGO.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0f); bRt.anchorMax = new Vector2(0.5f, 0f); bRt.pivot = new Vector2(0.5f, 0f);
            bRt.anchoredPosition = new Vector2(-70f, 18f); bRt.sizeDelta = new Vector2(140f, 40f);

            Image bBg = btnGO.GetComponent<Image>();
            bBg.color = new Color(0.26f, 0.52f, 0.96f, 1f);

            GameObject bTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            bTxtGO.transform.SetParent(btnGO.transform, false);
            RectTransform btRt = bTxtGO.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one; btRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI btTmp = bTxtGO.GetComponent<TextMeshProUGUI>();
            btTmp.text = "SIGN IN"; btTmp.fontSize = 15; btTmp.fontStyle = FontStyles.Bold;
            btTmp.alignment = TextAlignmentOptions.Center; btTmp.color = Color.white;

            Button signInBtn = btnGO.GetComponent<Button>();
            signInBtn.onClick.AddListener(() =>
            {
                string enteredName = nameInputField.text.Trim();
                string enteredEmail = emailInputField.text.Trim();

                if (string.IsNullOrEmpty(enteredName)) enteredName = "Mihir Jariwala";
                if (string.IsNullOrEmpty(enteredEmail)) enteredEmail = "mihirjariwala334@gmail.com";

                googleLoginModal.SetActive(false);
                UpdateStatus($"Signing in as {enteredEmail}...");
                if (loadingOverlay != null) loadingOverlay.SetActive(true);

                FirebaseManager.Instance.SignInWithRealGoogleCredentials(enteredEmail, enteredName, (success, msg) =>
                {
                    UpdateStatus(msg);
                    if (success)
                    {
                        StartCoroutine(RoutineLoadMainMenu(0.5f));
                    }
                    else
                    {
                        if (loadingOverlay != null) loadingOverlay.SetActive(false);
                    }
                });
            });

            // 7. Cancel Button
            GameObject cancelGO = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelGO.transform.SetParent(cardGO.transform, false);

            RectTransform cBtnRt = cancelGO.GetComponent<RectTransform>();
            cBtnRt.anchorMin = new Vector2(0.5f, 0f); cBtnRt.anchorMax = new Vector2(0.5f, 0f); cBtnRt.pivot = new Vector2(0.5f, 0f);
            cBtnRt.anchoredPosition = new Vector2(80f, 18f); cBtnRt.sizeDelta = new Vector2(120f, 40f);

            Image cBtnBg = cancelGO.GetComponent<Image>();
            cBtnBg.color = new Color(0.4f, 0.45f, 0.5f, 1f);

            GameObject cTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            cTxtGO.transform.SetParent(cancelGO.transform, false);
            RectTransform ctRt = cTxtGO.GetComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one; ctRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI ctTmp = cTxtGO.GetComponent<TextMeshProUGUI>();
            ctTmp.text = "CANCEL"; ctTmp.fontSize = 14; ctTmp.fontStyle = FontStyles.Bold;
            ctTmp.alignment = TextAlignmentOptions.Center; ctTmp.color = Color.white;

            Button cancelBtn = cancelGO.GetComponent<Button>();
            cancelBtn.onClick.AddListener(() =>
            {
                googleLoginModal.SetActive(false);
                UpdateStatus("Login cancelled");
            });

            googleLoginModal.SetActive(true);
            googleLoginModal.transform.SetAsLastSibling();
        }

        public void OnGuestLoginClicked()
        {
            UpdateStatus("Signing in as Guest...");
            if (loadingOverlay != null) loadingOverlay.SetActive(true);

            EnsureFirebaseInstance();

            FirebaseManager.Instance.SignInAnonymously((success, message) =>
            {
                UpdateStatus(message);
                if (success)
                {
                    StartCoroutine(RoutineLoadMainMenu(0.5f));
                }
                else
                {
                    if (loadingOverlay != null) loadingOverlay.SetActive(false);
                }
            });
        }

        private void EnsureFirebaseInstance()
        {
            if (FirebaseManager.Instance == null)
            {
                GameObject fbGO = new GameObject("FirebaseManager", typeof(FirebaseManager));
                DontDestroyOnLoad(fbGO);
            }
        }

        private IEnumerator RoutineLoadMainMenu(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Load target main menu scene
            string nextScene = !string.IsNullOrEmpty(targetNextScene) ? targetNextScene : "MainMenu";
            
            // Fallback check if MainMenu vs MainMenuScene exists
            if (Application.CanStreamedLevelBeLoaded(nextScene))
            {
                SceneManager.LoadScene(nextScene);
            }
            else if (Application.CanStreamedLevelBeLoaded("MainMenuScene"))
            {
                SceneManager.LoadScene("MainMenuScene");
            }
            else
            {
                SceneManager.LoadScene(1); // Index 1
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[LoginSceneController] {message}");
        }
    }
}
