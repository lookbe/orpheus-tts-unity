using LlamaCpp;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BasicTTS : MonoBehaviour
{
    public OrpheusTTS.OrpheusTTS tts;

    public TMP_InputField chatInputField;
    public Button sendButton;

    void Start()
    {
        tts.InitModel();
        sendButton.onClick.AddListener(OnSendButtonClicked);
    }

    private void OnEnable()
    {
        if (tts != null)
        {
            tts.OnStatusChanged += OnBotStatusChanged;

            OnBotStatusChanged(tts.status);
        }
    }

    private void OnDisable()
    {
        if (tts != null)
        {
            tts.OnStatusChanged -= OnBotStatusChanged;
        }
    }

    void OnBotStatusChanged(ModelStatus status)
    {
        switch (status)
        {
            case ModelStatus.Loading:
                {
                    sendButton.interactable = false;
                }
                break;
            case ModelStatus.Ready:
                {
                    sendButton.interactable = true;
                    ClearInput();
                }
                break;
            case ModelStatus.Generate:
                {
                    sendButton.interactable = false;
                }
                break;
            case ModelStatus.Error:
                {
                    sendButton.interactable = true;
                }
                break;
        }
    }

    protected virtual void ClearInput()
    {
        chatInputField.text = "";
    }

    public void OnSendButtonClicked()
    {
        if (tts)
        {
            string message = chatInputField.text;
            if (!string.IsNullOrEmpty(message))
            {
                tts.Prompt(message);
                ClearInput();
            }
        }
    }
}
