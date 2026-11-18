using System;
namespace UnityEngine {
 public class Object { public string name; public static void Destroy(Object o){} public static void DontDestroyOnLoad(Object o){} public static T Instantiate<T>(T o) where T:Object=>o; public static T Instantiate<T>(T o, Transform p) where T:Object=>o; public static T Instantiate<T>(T o, Transform p, bool w) where T:Object=>o; public static implicit operator bool(Object o)=>o!=null; }
 public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>(){return default(T);} public T GetComponentInChildren<T>(){return default(T);} }
 public class Behaviour : Component { public bool enabled; }
 public class MonoBehaviour : Behaviour { public void StopAllCoroutines(){} public Coroutine StartCoroutine(System.Collections.IEnumerator e){return null;} public void StopCoroutine(Coroutine c){} public void Invoke(string n,float t){} }
 public class Coroutine{}
 public class GameObject : Object { public void SetActive(bool b){} public bool activeSelf; public T AddComponent<T>() where T:Component,new(){return new T();} public T GetComponent<T>(){return default(T);} }
 public class Transform : Component { public int childCount; public Transform GetChild(int i){return null;} public Transform parent; public System.Collections.IEnumerator GetEnumerator(){return null;} }
 public class RectTransform : Transform {}
 public class SerializeField : Attribute {}
 public class HeaderAttribute : Attribute { public HeaderAttribute(string s){} }
 public class TooltipAttribute : Attribute { public TooltipAttribute(string s){} }
 public class RangeAttribute : Attribute { public RangeAttribute(float a,float b){} }
 public class RequireComponent : Attribute { public RequireComponent(Type t){} }
 public class AudioClip : Object { public float length; }
 public class AudioSource : Behaviour { public AudioClip clip; public float volume; public bool loop; public bool mute; public bool isPlaying; public void Play(){} public void Stop(){} public void PlayOneShot(AudioClip c){} public void PlayOneShot(AudioClip c,float v){} }
 public class Sprite : Object {} public class Texture2D : Object {}
 public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white=new Color(1,1,1); public static Color black=new Color(0,0,0); public static Color red=new Color(1,0,0); public static Color green=new Color(0,1,0); public static Color gray=new Color(.5f,.5f,.5f); public static Color yellow=new Color(1,1,0); }
 public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
 public static class Debug { public static void Log(object o){} public static void LogWarning(object o){} public static void LogError(object o){} }
 public static class Mathf { public static int Max(int a,int b)=>a>b?a:b; public static float Max(float a,float b)=>a>b?a:b; public static int Min(int a,int b)=>a<b?a:b; public static float Min(float a,float b)=>a<b?a:b; public static int Clamp(int v,int a,int b)=>v<a?a:v>b?b:v; public static float Clamp(float v,float a,float b)=>v<a?a:v>b?b:v; public static float Clamp01(float v)=>v<0?0:v>1?1:v; public static int RoundToInt(float f)=>(int)Math.Round(f); public static int FloorToInt(float f)=>(int)Math.Floor(f); public static int CeilToInt(float f)=>(int)Math.Ceiling(f); public static float Abs(float f)=>Math.Abs(f); public static int Abs(int f)=>Math.Abs(f); public static float Lerp(float a,float b,float t)=>a+(b-a)*t; public static float Pow(float a,float b)=>(float)Math.Pow(a,b); public static float Sqrt(float a)=>(float)Math.Sqrt(a); public static float Log(float a)=>(float)Math.Log(a); public static float Exp(float a)=>(float)Math.Exp(a); public static float Floor(float a)=>(float)Math.Floor(a); }
 public static class Random { public static float value=>0.5f; public static int Range(int a,int b)=>a; public static float Range(float a,float b)=>a; }
 public static class Time { public static float time, deltaTime, unscaledDeltaTime, timeScale; }
 public static class Application { public static string persistentDataPath=""; public static void Quit(){} public static void OpenURL(string u){} public static string version=""; }
 public static class Screen { public static int width,height; }
 public static class AudioListener { public static float volume; public static bool pause; }
 public static class PlayerPrefs { public static void SetString(string k,string v){} public static string GetString(string k){return "";} public static string GetString(string k,string d){return d;} public static void SetInt(string k,int v){} public static int GetInt(string k){return 0;} public static int GetInt(string k,int d){return d;} public static void SetFloat(string k,float v){} public static float GetFloat(string k){return 0;} public static float GetFloat(string k,float d){return d;} public static bool HasKey(string k){return false;} public static void DeleteKey(string k){} public static void Save(){} }
 public static class JsonUtility { public static string ToJson(object o){return "";} public static string ToJson(object o,bool p){return "";} public static T FromJson<T>(string s){return default(T);} public static void FromJsonOverwrite(string s,object o){} }
 public static class Resources { public static T Load<T>(string p) where T:Object {return null;} public static T[] LoadAll<T>(string p) where T:Object {return null;} }
 public class TextAsset : Object { public string text; }
 namespace Events { public delegate void UnityAction(); public delegate void UnityAction<T0>(T0 a); public class UnityEvent { public void AddListener(UnityAction a){} public void RemoveListener(UnityAction a){} public void RemoveAllListeners(){} public void Invoke(){} } public class UnityEvent<T0> { public void AddListener(UnityAction<T0> a){} public void RemoveAllListeners(){} public void Invoke(T0 a){} } }
}
namespace UnityEngine.UI {
 public class Graphic : UnityEngine.Behaviour { public Color color; }
 public class Image : Graphic { public Sprite sprite; public float fillAmount; }
 public class Text : Graphic { public string text; public int fontSize; public bool supportRichText; }
 public class Selectable : UnityEngine.Behaviour { public bool interactable; }
 public class Button : Selectable { public UnityEngine.Events.UnityEvent onClick = new UnityEngine.Events.UnityEvent(); }
 public class Toggle : Selectable { public bool isOn; public void SetIsOnWithoutNotify(bool b){} public UnityEngine.Events.UnityEvent<bool> onValueChanged=new UnityEngine.Events.UnityEvent<bool>(); }
 public class Slider : Selectable { public float value,minValue,maxValue; public void SetValueWithoutNotify(float f){} public UnityEngine.Events.UnityEvent<float> onValueChanged=new UnityEngine.Events.UnityEvent<float>(); }
 public class InputField : Selectable { public string text; public UnityEngine.Events.UnityEvent<string> onEndEdit=new UnityEngine.Events.UnityEvent<string>(); public UnityEngine.Events.UnityEvent<string> onValueChanged=new UnityEngine.Events.UnityEvent<string>(); public string placeholder; public enum ContentType{Standard,IntegerNumber,Password} public ContentType contentType; }
 public class Dropdown : Selectable { public int value; public System.Collections.Generic.List<OptionData> options=new System.Collections.Generic.List<OptionData>(); public UnityEngine.Events.UnityEvent<int> onValueChanged=new UnityEngine.Events.UnityEvent<int>(); public void ClearOptions(){} public void AddOptions(System.Collections.Generic.List<string> o){} public void RefreshShownValue(){} public class OptionData{ public string text; public OptionData(string t){text=t;} } }
 public class ScrollRect : UnityEngine.Behaviour {}
}
namespace UnityEngine { public class WaitForSeconds { public WaitForSeconds(float s){} } public partial class Object2{} }
