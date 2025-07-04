using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class StartButton : MonoBehaviour
{
    [SerializeField] private GameObject startWindow;
    [SerializeField] private MainMenu mainMenu;
    [SerializeField] private Toggle deckTransformationToggle;
    [SerializeField] private TMP_InputField parameterInput;
    
    public void OnClick()
    {
        startWindow.SetActive(false);
        mainMenu.InitializeExample(GetComponentInChildren<TMP_Text>().text, deckTransformationToggle.isOn); 
    }

    public void OnClickParametrized()
    {
        startWindow.SetActive(false);
        mainMenu.Initialize(parameterInput.text, deckTransformationToggle.isOn, true);
    }
}