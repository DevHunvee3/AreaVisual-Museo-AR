using easyar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;
using VideoPlayer = UnityEngine.Video.VideoPlayer;

public class Manager : MonoBehaviour
{    
    public GameObject readyButton;
    public GameObject WaitMessage;
    public GameObject mainVideoPlayerObject;
    public Camera ARCamera;
    public GameObject AR;
    public GameObject ImageTargetPrefab;
    public ImageTrackerBehaviour imageTrackerBehaviour;
    private VideoPlayer _mainPlayer;
    //private AudioSource _mainAudio;
    
    public double time;
    public bool Begin = false;
    bool prepared = false;
    [Serializable]
    class Status
    {
        public bool begin;
        public int readyDevices;
    }
    [Serializable]
     class Hologram
    {
        public string video;
        public string marcador;
        public string inicio;
        public string duracion;
        public float ancho;
        public float alto;
        public float pos_x;
        public float pos_y;
        public float pos_z;
    }

    [Serializable]
     class Configuration
    {
        public string videoPrincipal;
        public Hologram[] hologramas;
    }

    public double StringToSeconds(string s) {
        string[] ss = s.Split(':');
        if (ss.Length == 2) {
            return (double.Parse(ss[0]) + (double.Parse(ss[1]) / 60f)) * 60;
        }
        return 0;
    }

    List<ImageTargetController> targetControllers = new List<ImageTargetController>();
    public async void Awake(){
        _mainPlayer = mainVideoPlayerObject.GetComponent<VideoPlayer>();
        //_mainAudio = mainVideoPlayerObject.GetComponent<AudioSource>();

        Configuration configuration = null ;
        configuration = await fetchConfiguration();

        if (configuration != null) {
            _mainPlayer.source = VideoSource.Url;
            _mainPlayer.url = Application.streamingAssetsPath + "/Videos/" + configuration.videoPrincipal;
            _mainPlayer.Prepare();
            _mainPlayer.SetDirectAudioMute(0, false);
            foreach (Hologram hologram in configuration.hologramas)
            {
                GameObject target = Instantiate<GameObject>(ImageTargetPrefab,AR.transform);
                ImageTargetController targetController = target.GetComponent<ImageTargetController>();
                
                targetController.ImageTracker = imageTrackerBehaviour;
                targetController.TargetName = hologram.marcador;
                targetController.TargetPath = "/Marcadores/" + hologram.marcador;
                GameObject child = target.transform.GetChild(0).gameObject;
                //configurar posicion relativa al marcador y tamaño segund configuraciones
                VideoPlayer hologramPlayer = child.GetComponent<VideoPlayer>();
                hologramPlayer.source = VideoSource.Url;
                hologramPlayer.url = Application.streamingAssetsPath + "/Videos/" + hologram.video;                
                
                targetController.duration = StringToSeconds(hologram.duracion);                
                targetController.initialTime = StringToSeconds(hologram.inicio);
                targetControllers.Add(targetController);
            }

            readyButton.SetActive(false);
            WaitMessage.SetActive(false);
            AR.SetActive(true);
        }
    }
    public List<ImageTargetController> dispose;
    public void Update(){
        if(_mainPlayer.isPrepared && !prepared){
            prepared=true;
            WaitMessage.SetActive(false);
            readyButton.SetActive(true);
        }
        bool arCameraOn = false;
        if (targetControllers.Count > 0 && _mainPlayer.isPlaying)
        {
            foreach (ImageTargetController targetController in targetControllers)
            {
                double overallTime = _mainPlayer.time;
                targetController.overallTime = overallTime;
                if (overallTime >= targetController.initialTime && overallTime <= targetController.initialTime + targetController.duration)
                {
                    arCameraOn = true;

                }
                if (overallTime > targetController.initialTime + targetController.duration) {
                    dispose.Add(targetController);
                }
            }
        }

        foreach (ImageTargetController go in dispose) {
            targetControllers.Remove(go);            
            Destroy(go.gameObject);
        }
        dispose.Clear();

        if (arCameraOn) 
            ARCamera.depth = 1;        
        else
            ARCamera.depth = -1;
        

        //DEBUG-DEVELOPMENT
        if (Input.GetKeyDown(KeyCode.F11)) {
            FastForward(1);
        }
        if (Input.GetKeyDown(KeyCode.F12))
        {
            FastForward(10);
        }
        time = _mainPlayer.time;
    }

    private async Task<Configuration> fetchConfiguration()
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://192.168.0.60:8888/getConfiguration.php");
        HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
        StreamReader reader = new StreamReader(response.GetResponseStream());
        string jsonResponse = reader.ReadToEnd();
        return JsonUtility.FromJson<Configuration>(jsonResponse);
    }

    public async void onReady()
    {
        //inform to db that we are ready
        //On response Do:
        Status response = new Status();
        
        response.begin = false;
        response.readyDevices = 0;
        await setReady();
        WaitMessage.SetActive(true);
        readyButton.SetActive(false);
        while (!response.begin)
        {
            response = await fetchStatus();
            await Task.Delay(250);
        }
        //In order to keep everybody in sync it was decided not to use a different scene to avoid loading time loss.
        readyButton.SetActive(false);
        WaitMessage.SetActive(false);
        _mainPlayer.enabled = true;
        _mainPlayer.Play();
        //_mainAudio.Play();
        //play the video
        //poll video time in order to get AR video going.
    }

    private async Task<Status> fetchStatus(){
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://192.168.0.60:8888/getStatus.php");
        HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
        StreamReader reader = new StreamReader(response.GetResponseStream());
        string jsonResponse = reader.ReadToEnd();

        return JsonUtility.FromJson<Status>(jsonResponse);        
    }

    private async Task setReady(){
        //This api call increments the amount of devices that are ready
        //the purpose is to show to the admin app how many devices are ready to run in order to decide if the experince should be started or not.
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://192.168.0.60:8888/setReady.php");                
        HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
        return;        
    }

    public void FastForward(int time)
    {
        if (!_mainPlayer.isPrepared)
        {
            Debug.Log("Video Not Prepared.");
            return;
        }
        _mainPlayer.time += 60*time; //+10 mins
        //_mainAudio.time += 60 * time;
    }
}
