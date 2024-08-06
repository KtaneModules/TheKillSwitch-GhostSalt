using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class KillSwitchScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public MeshRenderer[] LEDs;
    public Material[] Mats;
    public GameObject[] Glows;
    public TextMesh[] LabelTexts;

    private Coroutine[] ButtonAnimCoroutines = new Coroutine[3];
    private const string Hexadecimal = "0123456789ABCDEF";
    private const float LabelRotationVariance = 1f;
    private List<int> SwitchValues = new List<int>();
    private List<int> LabelValues = new List<int>();
    private int KillSwitch, SolveCache;
    private bool Solved;

    private string ToTernary(int num)
    {
        if (num == 0)
            return "0";
        else
        {
            string ternaryNum = "";
            int current = 0;
            while (num != 0)
            {
                current = num % 3;
                ternaryNum = current.ToString() + ternaryNum;
                num = num / 3;
            }
            return ternaryNum;
        }
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        LabelValues = Enumerable.Range(0, Hexadecimal.Count()).ToList().Shuffle().Take(3).ToList();

        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate { ButtonPress(x); return false; };
            Glows[x].SetActive(false);
            LEDs[x].material = Mats[0];
            LabelTexts[x].text = Hexadecimal[LabelValues[x]].ToString();
            LabelTexts[x].transform.parent.localEulerAngles = Vector3.up * Rnd.Range(-LabelRotationVariance, LabelRotationVariance);
        }
    }

    // Use this for initialization
    void Start ()
    {
        KillSwitch = CalculateKS();
        Debug.LogFormat("[The Kill Switch #{0}] This means that the kill switch is Button {1}.", _moduleID, KillSwitch + 1);
    }
	
	// Update is called once per frame
	void Update ()
    {
        if (Bomb.GetSolvedModuleIDs().Count() != SolveCache)
            SolveCache = Bomb.GetSolvedModuleIDs().Count();
    }

    private int CalculateKS()
    {
        SwitchValues = new List<int>() { Bomb.GetBatteryCount() % 3, Bomb.GetIndicators().Count() % 3, Bomb.GetPortCount() % 3 };
        Debug.LogFormat("[The Kill Switch #{0}] There {1} {2} batter{3}, {4} indicator{5} and {6} port{7}, so 𝑥₁₋₃ = {8}.", _moduleID, Bomb.GetBatteryCount() == 1 ? "is" : "are", Bomb.GetBatteryCount(), Bomb.GetBatteryCount() == 1 ? "y" : "ies",
            Bomb.GetIndicators().Count(), Bomb.GetIndicators().Count() == 1 ? "" : "s",
            Bomb.GetPortCount(), Bomb.GetPortCount() == 1 ? "" : "s",
            SwitchValues.Join(", "));
        var d = ToTernary(LabelValues.Sum());     // This converts the sum into ternary. Handy!
        Debug.LogFormat("[The Kill Switch #{0}] The labels are {1}, so their sum is {2} and D = {3}.", _moduleID, LabelValues.Select(x => Hexadecimal[x]).Join(", "), LabelValues.Sum(), d);

        if (SwitchValues[0] == SwitchValues[1] && SwitchValues[1] == SwitchValues[2])
        {
            Debug.LogFormat("[The Kill Switch #{0}] All three values of 𝑥ₙ are equal, so rule 1 applies.", _moduleID);
            return SwitchValues[0];
        }

        if (SwitchValues[0] != SwitchValues[1] && SwitchValues[1] != SwitchValues[2] && SwitchValues[2] != SwitchValues[0])     // I'm aware that there are better ways of doing this, but I think this is easier to read.
        {
            var matches = SwitchValues.Where(x => d.Contains(x.ToString())).ToList();
            if (matches.Count() == 1)
            {
                Debug.LogFormat("[The Kill Switch #{0}] All three values of 𝑥ₙ are distinct and one of them appears in D's digits, so rule 2a applies.", _moduleID);
                return SwitchValues.IndexOf(matches.First());
            }
            if (matches.Count() == 2)
            {
                Debug.LogFormat("[The Kill Switch #{0}] All three values of 𝑥ₙ are distinct and one of them does not appear in D's digits, so rule 2b applies.", _moduleID);
                return SwitchValues.FirstOrDefault(x => !matches.Contains(x));
            }
            Debug.LogFormat("[The Kill Switch #{0}] All three values of 𝑥ₙ are distinct and all of them appear in D's digits, so rule 2c applies.", _moduleID);
            return int.Parse(d.Last().ToString());
        }

        if (SwitchValues.Where(x => x == SwitchValues.Max()).Count() == 1)
        {
            Debug.LogFormat("[The Kill Switch #{0}] The highest value of 𝑥ₙ is distinct, so rule 3 applies.", _moduleID);
            return (int.Parse(d.Last().ToString()) + SwitchValues.Max()) % 3;
        }

        if (SwitchValues.Min() == 0)
        {
            Debug.LogFormat("[The Kill Switch #{0}] The lowest value of 𝑥ₙ is 0, so rule 4 applies.", _moduleID);
            return (int.Parse(d.First().ToString()) + int.Parse(d.Last().ToString())) % 3;
        }

        Debug.LogFormat("[The Kill Switch #{0}] Rules 1-4 do not apply, so rule 5 automatically applies.", _moduleID);
        return int.Parse(d.First().ToString());
    }

    void ButtonPress(int pos)
    {
        Audio.PlaySoundAtTransform("press", Buttons[pos].transform);
        Buttons[pos].AddInteractionPunch();
        if (ButtonAnimCoroutines[pos] != null)
            StopCoroutine(ButtonAnimCoroutines[pos]);
        ButtonAnimCoroutines[pos] = StartCoroutine(ButtonPressAnim(pos));
        if (!Solved)
        {
            Audio.PlaySoundAtTransform("buzz", Module.transform);
            Module.HandlePass();
            Solved = true;
            LEDs[pos].material = Mats[1];
            Glows[pos].SetActive(true);
            if (pos == KillSwitch)
            {
                Debug.LogFormat("[The Kill Switch #{0}] You pressed Button {1}, which is the kill switch. Module solved, but expect a strike fairly soon…", _moduleID, pos + 1);
                StartCoroutine(WaitForStrike());
            }
            else
                Debug.LogFormat("[The Kill Switch #{0}] You pressed Button {1}, which is not the kill switch. Module solved!", _moduleID, pos + 1);
        }
    }

    private IEnumerator WaitForStrike()
    {
        var waitTime = Rnd.Range(120f, 1200f);
        var numModules = Bomb.GetSolvableModuleIDs().Count();
        float timer = 0;
        while (timer < waitTime && SolveCache < numModules)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Debug.LogFormat("[The Kill Switch #{0}] Kapow! Strike!", _moduleID);
        Module.HandleStrike();
        yield return "strike";
    }

    private IEnumerator ButtonPressAnim(int pos, float interval = 0.1f, float start = -0.00518f, float end = -0.00858f)
    {
        float timer = 0;
        while (timer < interval)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Buttons[pos].transform.localPosition.y, Easing.InSine(timer, start, end, interval));
        }
        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Buttons[pos].transform.localPosition.y, end);
        timer = 0;
        while (timer < 0.075f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        timer = 0;
        while (timer < interval)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Buttons[pos].transform.localPosition.y, Easing.InSine(timer, end, start, interval));
        }
        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Buttons[pos].transform.localPosition.y, start);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} press 1-3' to press one of the three buttons. Buttons are ordered in reading order.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        string[] CommandArray = command.Split(' ');
        if (CommandArray.Length != 2 || CommandArray[0] != "press")
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
        bool bad = false;
        try
        {
            int.Parse(CommandArray[1]);
        }
        catch (FormatException e)
        {
            bad = true;
        }
        if (bad)
        {
            yield return "sendtochaterror Invalid command — “" + CommandArray[1] + "” is not a number that I can make sense of.";
            yield break;
        }
        var num = int.Parse(CommandArray[1]);
        if (num < 1 || num > 3)
        {
            yield return "sendtochaterror Invalid command — button position is out of range.";
            yield break;
        }
        yield return null;
        Buttons[int.Parse(CommandArray[1]) - 1].OnInteract();
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        if (KillSwitch == 0)
            Buttons[1].OnInteract();
        else
            Buttons[0].OnInteract();
    }
}
