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
    [Header("Configuración General")]
    public VideoPlayer mainVideoPlayer;    
    public Camera ARCamera;
    public GameObject AR;

    [Header("Configuración de Marcadores")]
    public GameObject ImageTargetPrefab;
    public ImageTrackerBehaviour imageTrackerBehaviour;

    [Header("Configuración Gráfica")]
    public GameObject readyButton;
    public GameObject waitMessage;
    public GameObject qrUi;

    [Header("Configuración de Conexión")]
    public GConfiguration globalConfiguration;

    [Header("Otros")]
    public double time;
    bool begin = false;
    bool prepared = false;
    

    List<ImageTargetController> targetControllers = new List<ImageTargetController>();
    List<ImageTargetController> dispose = new List<ImageTargetController>();

    [Serializable]
    public class Status
    {
        public bool begin;
        public int readyDevices;
    }
    [Serializable]
    public class Hologram
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
    public class Configuration
    {
        public string videoPrincipal;
        public Hologram[] hologramas;
    }
    [Serializable]
    public class GConfiguration {
        public string serverIp;
        public string port;
    }

    
    public async void Awake(){               

        Configuration configuration = null ;        
        globalConfiguration = await fetchGConfiguration();
        configuration = await fetchConfiguration(globalConfiguration);

        if (configuration != null) {
            mainVideoPlayer.source = VideoSource.Url;
            mainVideoPlayer.url = Application.streamingAssetsPath + "/Videos/" + configuration.videoPrincipal;
            mainVideoPlayer.Prepare();
            mainVideoPlayer.SetDirectAudioMute(0, false);
            foreach (Hologram hologram in configuration.hologramas)
            {
                //Create new Target Object, Get target Player, and get TargetController
                GameObject target = Instantiate(ImageTargetPrefab,AR.transform);
                Transform targetTransform = target.transform.GetChild(0);
                ImageTargetController targetController = target.GetComponent<ImageTargetController>();
                VideoPlayer targetPlayer = targetController.videoPlayer;
                
                //Setup targetController
                targetController.ImageTracker = imageTrackerBehaviour;
                targetController.qrUi = qrUi;
                targetController.TargetName = hologram.marcador;
                targetController.TargetPath = "/Marcadores/" + hologram.marcador;
                targetController.duration = StringToSeconds(hologram.duracion);                
                targetController.initialTime = StringToSeconds(hologram.inicio);
                //configurar posicion relativa al marcador y tamaño segund configuraciones - targetTransform.

                //Setup targetPlayer
                targetPlayer.source = VideoSource.Url;
                targetPlayer.url = Application.streamingAssetsPath + "/Videos/" + hologram.video;
                targetPlayer.Prepare();

                targetControllers.Add(targetController);
            }

            readyButton.SetActive(false);
            waitMessage.SetActive(false);
            AR.SetActive(true);
        }
    }
    
    public void Update(){
        if(mainVideoPlayer.isPrepared && !prepared){
            prepared=true;
            waitMessage.SetActive(false);
            readyButton.SetActive(true);
        }
        bool arCameraOn = false;
        if (targetControllers.Count > 0 && mainVideoPlayer.isPlaying)
        {
            foreach (ImageTargetController targetController in targetControllers)
            {
                double overallTime = mainVideoPlayer.time;
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
        time = mainVideoPlayer.time;
    }

    private async Task<Configuration> fetchConfiguration(GConfiguration gConfig)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://"+gConfig.serverIp+":"+gConfig.port+"/getConfiguration.php");
        HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
        StreamReader reader = new StreamReader(response.GetResponseStream());
        string jsonResponse = reader.ReadToEnd();
        return JsonUtility.FromJson<Configuration>(jsonResponse);
    }

    private async Task<GConfiguration> fetchGConfiguration()
    {
        string jsonResponse = File.ReadAllText(Application.streamingAssetsPath + "/Configuraciones/configuracion.json");        
        return JsonUtility.FromJson<GConfiguration>(jsonResponse);
    }

    public async void onReady()
    {
        //inform to db that we are ready
        //On response Do:
        Status response = new Status();
        
        response.begin = false;
        response.readyDevices = 0;
        await setReady(globalConfiguration);
        waitMessage.SetActive(true);
        readyButton.SetActive(false);
        while (!response.begin)
        {
            response = await fetchStatus(globalConfiguration);
            await Task.Delay(250);
        }
        //In order to keep everybody in sync it was decided not to use a different scene to avoid loading time loss.
        readyButton.SetActive(false);
        waitMessage.SetActive(false);
        mainVideoPlayer.enabled = true;
        mainVideoPlayer.Play();
        //_mainAudio.Play();
        //play the video
        //poll video time in order to get AR video going.
    }

    private async Task<Status> fetchStatus(GConfiguration gConfig)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + gConfig.serverIp + ":" + gConfig.port + "/getStatus.php");
        HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
        StreamReader reader = new StreamReader(response.GetResponseStream());
        string jsonResponse = reader.ReadToEnd();

        return JsonUtility.FromJson<Status>(jsonResponse);        
    }

    private async Task setReady(GConfiguration gConfig)
    {
        //This api call increments the amount of devices that are ready
        //the purpose is to show to the admin app how many devices are ready to run in order to decide if the experince should be started or not.
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + gConfig.serverIp + ":" + gConfig.port + "/setReady.php");                
        HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync());
        return;        
    }

    public void FastForward(int time)
    {
        if (!mainVideoPlayer.isPrepared)
        {
            Debug.Log("Video Not Prepared.");
            return;
        }
        mainVideoPlayer.time += 60*time; //+10 mins
        //_mainAudio.time += 60 * time;
    }

    public double StringToSeconds(string s)
    {
        string[] ss = s.Split(':');
        if (ss.Length == 2)
        {
            return (double.Parse(ss[0]) + (double.Parse(ss[1]) / 60f)) * 60;
        }
        return 0;
    }

}
