using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace HollowCraft
{
    internal sealed class MinecraftAudio : MonoBehaviour
    {
        private sealed class Sample {internal AudioClip Clip;internal float Used;}
        private sealed class Voice {internal AudioSource Source;internal Vector2 Position;internal float Volume,Distance,Started;internal bool Relative,Linear,Active;}
        private readonly Dictionary<string,Sample> samples=new Dictionary<string,Sample>();
        private readonly HashSet<string> pending=new HashSet<string>();
        private readonly Queue<string> requests=new Queue<string>();
        private readonly Dictionary<string,Queue<JObject>> waiting=new Dictionary<string,Queue<JObject>>();
        private readonly Dictionary<string,float> failedUntil=new Dictionary<string,float>();
        private readonly List<Voice> voices=new List<Voice>();
        private HollowCraftRuntime runtime;
        private string directory;
        private int loading;
        private bool paused;
        internal void Initialize(HollowCraftRuntime owner,string assets) {runtime=owner;directory=Path.Combine(assets,"mc_audio");}
        internal void Receive(JObject message) {
            string key=(string)message["key"];
            if(string.IsNullOrEmpty(key)||!Regex.IsMatch(key,"\\A[0-9a-f]{64}\\z"))return;
            if(samples.TryGetValue(key,out Sample sample)&&sample.Clip!=null) {
                sample.Used=Time.unscaledTime;
                if((bool?)message["play"]==true)Play(sample.Clip,message);
                else Ready(key,true);
            } else {
                if(failedUntil.TryGetValue(key,out float retry)&&Time.unscaledTime<retry) {Ready(key,false);return;}
                if(!pending.Contains(key)&&requests.Count>=32) {Ready(key,false);return;}
                if((bool?)message["play"]==true) {
                    if(!waiting.TryGetValue(key,out Queue<JObject> plays))waiting[key]=plays=new Queue<JObject>();
                    if(plays.Count<8)plays.Enqueue(message);
                }
                if(requests.Count<32&&pending.Add(key))requests.Enqueue(key);
            }
        }
        private void Ready(string key,bool ready) {runtime?.Send(new {type="audioFile",scene=runtime.Room,key,ready});}
        private IEnumerator Load(string key) {
            string path=Path.Combine(directory,key+".ogg");
            try {
                if(!File.Exists(path))yield break;
                using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.OGGVORBIS)) {
                    yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success)yield break;
                    var clip=DownloadHandlerAudioClip.GetContent(request);if(clip==null||clip.length<=0) {if(clip!=null)Destroy(clip);yield break;}
                    clip.name="HollowCraft."+key;
                    if(samples.Count>=128) {
                        var oldest=samples.Where(entry=>!voices.Any(voice=>(voice.Active||voice.Source.isPlaying)&&voice.Source.clip==entry.Value.Clip)).OrderBy(entry=>entry.Value.Used).FirstOrDefault();
                        if(oldest.Key!=null) {Ready(oldest.Key,false);Destroy(oldest.Value.Clip);samples.Remove(oldest.Key);}
                    }
                    samples[key]=new Sample {Clip=clip,Used=Time.unscaledTime};Ready(key,true);
                    if(waiting.TryGetValue(key,out Queue<JObject> plays))while(plays.Count>0) {
                        var message=plays.Dequeue();
                        if((string)message["scene"]==runtime.Room&&(string)message["campaign"]==runtime.CampaignId)Play(clip,message);
                    }
                }
            } finally {
                if(!samples.ContainsKey(key)) {failedUntil[key]=Time.unscaledTime+15;Ready(key,false);}
                waiting.Remove(key);pending.Remove(key);loading--;
            }
        }
        private Voice NewVoice() {
            var node=new GameObject("HollowCraft.MinecraftSound");node.transform.SetParent(transform,false);
            var source=node.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=false;
            var voice=new Voice {Source=source};voices.Add(voice);return voice;
        }
        private void Play(AudioClip clip,JObject message) {
            float volume=(float?)message["volume"]??0,pitch=(float?)message["pitch"]??1,x=(float?)message["x"]??0,y=(float?)message["y"]??0,distance=(float?)message["distance"]??16;
            if(float.IsNaN(volume)||float.IsNaN(pitch)||float.IsNaN(x)||float.IsNaN(y)||float.IsInfinity(x)||float.IsInfinity(y))return;
            var voice=voices.FirstOrDefault(entry=>!entry.Active&&!entry.Source.isPlaying)??(voices.Count<128?NewVoice():voices.OrderBy(entry=>entry.Source.volume).ThenBy(entry=>entry.Started).First());
            voice.Source.Stop();voice.Source.clip=clip;voice.Source.pitch=Mathf.Clamp(pitch,.5f,2);
            voice.Position=new Vector2(x,y);voice.Volume=Mathf.Clamp01(volume);voice.Distance=Math.Max(.01f,distance);
            voice.Relative=(bool?)message["relative"]??false;voice.Linear=(bool?)message["linear"]??true;voice.Started=Time.unscaledTime;voice.Active=true;
            Apply(voice);voice.Source.Play();if(paused)voice.Source.Pause();
        }
        private void Apply(Voice voice) {
            var hero=HeroController.instance;Vector2 delta=hero==null?Vector2.zero:voice.Position-GameCursor.Eye(hero);
            if(voice.Source.outputAudioMixerGroup==null&&hero!=null)
                voice.Source.outputAudioMixerGroup=hero.GetComponentsInChildren<AudioSource>().Select(source=>source.outputAudioMixerGroup).FirstOrDefault(group=>group!=null);
            float volume=voice.Volume;
            if(!voice.Relative&&voice.Linear)volume*=Mathf.Clamp01(1-delta.magnitude/voice.Distance);
            // Existing hero mixer routing follows native SFX settings. Before it exists, use
            // the native slider range rather than playing a new unattenuated master source.
            if(voice.Source.outputAudioMixerGroup==null&&GameManager.instance?.gameSettings!=null)volume*=Mathf.Clamp01(GameManager.instance.gameSettings.soundVolume/10);
            voice.Source.volume=volume;voice.Source.panStereo=voice.Relative?0:Mathf.Clamp(delta.x/Math.Max(16,voice.Distance),-1,1)*.75f;
        }
        private void Update() {
            if(runtime==null)return;
            foreach(string key in failedUntil.Keys.Where(key=>Time.unscaledTime>=failedUntil[key]).ToArray())failedUntil.Remove(key);
            while(loading<2&&requests.Count>0) {loading++;StartCoroutine(Load(requests.Dequeue()));}
            bool next=runtime.AudioPaused;
            if(paused!=next) {paused=next;foreach(var voice in voices)if(paused)voice.Source.Pause();else voice.Source.UnPause();}
            foreach(var voice in voices) {
                if(voice.Source.isPlaying)Apply(voice);
                else if(!paused)voice.Active=false;
            }
        }
        internal void StopAll() {waiting.Clear();foreach(var voice in voices) {voice.Source.Stop();voice.Active=false;}}
        private void OnDestroy() {StopAll();foreach(var sample in samples.Values)if(sample.Clip!=null)Destroy(sample.Clip);samples.Clear();}
    }
}
