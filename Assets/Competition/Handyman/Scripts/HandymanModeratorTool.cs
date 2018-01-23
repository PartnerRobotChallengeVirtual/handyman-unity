using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SIGVerse.Common;
using SIGVerse.ToyotaHSR;
using System.Collections;

namespace SIGVerse.Competition.Handyman
{
	[Serializable]
	public class RelocatableObjectInfo
	{
		public string name;
		public Vector3 position;
		public Vector3 eulerAngles;
	}

	[Serializable]
	public class EnvironmentInfo
	{
		public string environmentName;
		public string graspingTargetName;
		public List<RelocatableObjectInfo> graspablesPositions;
	}

	public class HandymanModeratorTool
	{
		private const string AreaNameBedRoom = "BedRoomArea";
		private const string AreaNameKitchen = "KitchenArea";
		private const string AreaNameLiving  = "LivingArea";
		private const string AreaNameLobby   = "LobbyArea";

		private const string RoomNameForTaskMessageKitchen = "kitchen";
		private const string RoomNameForTaskMessageLobby   = "lobby";
		private const string RoomNameForTaskMessageBedRoom = "bed room";
		private const string RoomNameForTaskMessageLiving  = "living room";

		private const string EnvironmentInfoFileNameFormat = "/../SIGVerseConfig/Handyman/EnvironmentInfo{0:D2}.json";


		private GameObject graspingTarget;
		private List<GameObject> graspables;
		private List<GameObject> graspingCandidates;

		private List<GameObject> graspingCandidatesPositions;

		private Transform hsrBaseFootPrint;
		private HSRGraspingDetector hsrGraspingDetector;

		private GameObject targetRoom;

		private GameObject bedRoomArea;
		private GameObject kitchenArea;
		private GameObject livingArea;
		private GameObject lobbyArea;

		private HandymanPlaybackPlayer   playbackPlayer;
		private HandymanPlaybackRecorder playbackRecorder;

		public HandymanModeratorTool(List<GameObject> environments)
		{
			HandymanConfig.Instance.InclementNumberOfTrials();

			EnvironmentInfo environmentInfo = this.EnableEnvironment(environments);

			this.GetGameObjects();

			this.Initialize(environmentInfo);
		}

		private EnvironmentInfo EnableEnvironment(List<GameObject> environments)
		{
			if(environments.Count != (from environment in environments select environment.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of environments.");
			}


			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				GameObject activeEnvironment = (from environment in environments where environment.activeSelf==true select environment).FirstOrDefault();

				if(activeEnvironment!=null)
				{
					environmentInfo.environmentName = activeEnvironment.name;

					SIGVerseLogger.Warn("Selected an active environment. name=" + activeEnvironment.name);
				}
				else
				{
					environmentInfo.environmentName = environments[UnityEngine.Random.Range(0, environments.Count)].name;
				}
			}
			else
			{
				environmentInfo = this.GetEnvironmentInfo();
			}

			foreach (GameObject environment in environments)
			{
				if(environment.name==environmentInfo.environmentName)
				{
					environment.SetActive(true);
				}
				else
				{
					environment.SetActive(false);
				}
			}

			return environmentInfo;
		}


		private void GetGameObjects()
		{
			GameObject robot = GameObject.FindGameObjectWithTag("Robot");

			this.hsrBaseFootPrint = HSRCommon.FindGameObjectFromChild(robot.transform, HSRCommon.BaseFootPrintName);
			this.hsrGraspingDetector = robot.GetComponentInChildren<HSRGraspingDetector>();


			// Get grasping candidates
			this.graspingCandidates = GameObject.FindGameObjectsWithTag("GraspingCandidates").ToList<GameObject>();


			if (this.graspingCandidates.Count == 0)
			{
				throw new Exception("Count of GraspingCandidates is zero.");
			}

			List<GameObject> dummyGraspingCandidates = GameObject.FindGameObjectsWithTag("DummyGraspingCandidates").ToList<GameObject>();

			this.graspables = new List<GameObject>();

			this.graspables.AddRange(this.graspingCandidates);
			this.graspables.AddRange(dummyGraspingCandidates);

			// Check the name conflict of graspables.
			if(this.graspables.Count != (from graspable in this.graspables select graspable.name).Distinct().Count())
			{
				throw new Exception("There is the name conflict of graspable objects.");
			}

			SIGVerseLogger.Info("Count of Graspables = " + this.graspables.Count);

			this.bedRoomArea = GameObject.Find(AreaNameBedRoom);
			this.kitchenArea = GameObject.Find(AreaNameKitchen);
			this.livingArea  = GameObject.Find(AreaNameLiving);
			this.lobbyArea   = GameObject.Find(AreaNameLobby);

			// Get grasping candidates positions
			this.graspingCandidatesPositions = GameObject.FindGameObjectsWithTag("GraspingCandidatesPosition").ToList<GameObject>();

			if (this.graspables.Count > this.graspingCandidatesPositions.Count)
			{
				throw new Exception("graspables.Count > graspingCandidatesPositions.Count.");
			}
			else
			{
				SIGVerseLogger.Info("Count of GraspingCandidatesPosition = " + this.graspingCandidatesPositions.Count);
			}
		}


		private void Initialize(EnvironmentInfo environmentInfo)
		{
			Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionMap = null; //key:GraspablePositionInfo, value:Graspables

			if(HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom)
			{
				this.graspingTarget = this.DecideGraspingTarget();

				graspablesPositionMap = this.CreateGraspablesPositionMap();

				this.SaveEnvironmentInfo(environmentInfo.environmentName, this.graspingTarget.name, graspablesPositionMap);
			}
			else
			{
				this.DeactivateGraspingCandidatesPositions();

				graspablesPositionMap = new Dictionary<RelocatableObjectInfo, GameObject>();

				this.graspingTarget = (from graspable in this.graspables where graspable.name == environmentInfo.graspingTargetName select graspable).First();

				if(this.graspingTarget==null) { throw new Exception("Grasping target not found. name=" + environmentInfo.graspingTargetName); }

				foreach(RelocatableObjectInfo graspablePositionInfo in environmentInfo.graspablesPositions)
				{
					GameObject graspableObj = (from graspable in this.graspables where graspable.name == graspablePositionInfo.name select graspable).First();

					if (graspableObj==null) { throw new Exception("Graspable object not found. name=" + graspablePositionInfo.name); }

					graspablesPositionMap.Add(graspablePositionInfo, graspableObj);
				}
			}

			foreach (KeyValuePair<RelocatableObjectInfo, GameObject> pair in graspablesPositionMap)
			{
				pair.Value.transform.position    = pair.Key.position;
				pair.Value.transform.eulerAngles = pair.Key.eulerAngles;

//				Debug.Log(pair.Key.name + " : " + pair.Value.name);
			}

			this.targetRoom = this.GetTargetRoom();
		}


		public void InitPlaybackVariables(GameObject worldPlayback)
		{
			this.playbackPlayer   = worldPlayback.GetComponent<HandymanPlaybackPlayer>();
			this.playbackRecorder = worldPlayback.GetComponent<HandymanPlaybackRecorder>();
		}


		public List<GameObject> GetGraspables()
		{
			return this.graspables;
		}


		public IEnumerator LoosenRigidbodyConstraints(Rigidbody rigidbody)
		{
			while(!rigidbody.IsSleeping())
			{
				yield return null;
			}

			rigidbody.constraints = RigidbodyConstraints.None;
		}


		public GameObject DecideGraspingTarget()
		{
			// Decide the grasping target and get the instance ID
			GameObject graspingTarget = this.graspingCandidates[UnityEngine.Random.Range(0, this.graspingCandidates.Count)];

			SIGVerseLogger.Info("Grasping target is " + graspingTarget.name);

			return graspingTarget;
		}


		public void DeactivateGraspingCandidatesPositions()
		{
			foreach (GameObject graspingCandidatesPosition in this.graspingCandidatesPositions)
			{
				graspingCandidatesPosition.SetActive(false);
			}
		}

		public Dictionary<RelocatableObjectInfo, GameObject> CreateGraspablesPositionMap()
		{
			List<GameObject> graspingCandidatesPositionsInBedRoom = new List<GameObject>();
			List<GameObject> graspingCandidatesPositionsInKitchen = new List<GameObject>();
			List<GameObject> graspingCandidatesPositionsInLiving  = new List<GameObject>();
			List<GameObject> graspingCandidatesPositionsInLobby   = new List<GameObject>();

			this.DeactivateGraspingCandidatesPositions();

			foreach (GameObject graspingCandidatesPosition in this.graspingCandidatesPositions)
			{
				Vector3 position = graspingCandidatesPosition.transform.position;

				if (this.IsTargetInArea(position, this.bedRoomArea)) { graspingCandidatesPositionsInBedRoom.Add(graspingCandidatesPosition); }
				if (this.IsTargetInArea(position, this.kitchenArea)) { graspingCandidatesPositionsInKitchen.Add(graspingCandidatesPosition); }
				if (this.IsTargetInArea(position, this.livingArea))  { graspingCandidatesPositionsInLiving .Add(graspingCandidatesPosition); }
				if (this.IsTargetInArea(position, this.lobbyArea))   { graspingCandidatesPositionsInLobby  .Add(graspingCandidatesPosition); }
			}

			// Shuffle the grasping candidates list
			this.graspables = this.graspables.OrderBy(i => Guid.NewGuid()).ToList();

			// Shuffle the grasping candidates position list
			graspingCandidatesPositionsInBedRoom = graspingCandidatesPositionsInBedRoom.OrderBy(i => Guid.NewGuid()).ToList();
			graspingCandidatesPositionsInKitchen = graspingCandidatesPositionsInKitchen.OrderBy(i => Guid.NewGuid()).ToList();
			graspingCandidatesPositionsInLiving  = graspingCandidatesPositionsInLiving .OrderBy(i => Guid.NewGuid()).ToList();
			graspingCandidatesPositionsInLobby   = graspingCandidatesPositionsInLobby  .OrderBy(i => Guid.NewGuid()).ToList();


			List<GameObject> graspingCandidatesPositionsTmp = new List<GameObject>();

			for (int i=0; graspingCandidatesPositionsTmp.Count < this.graspables.Count; i++)
			{
				if (graspingCandidatesPositionsInBedRoom.Count > i)
				{
					graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInBedRoom[i]);
				}

				if (graspingCandidatesPositionsInKitchen.Count > i)
				{
					graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInKitchen[i]);
				}

				if (graspingCandidatesPositionsInLiving.Count > i)
				{
					graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInLiving[i]);
				}

				if (graspingCandidatesPositionsInLobby.Count > i)
				{
					graspingCandidatesPositionsTmp.Add(graspingCandidatesPositionsInLobby[i]);
				}
			}

			Dictionary<RelocatableObjectInfo, GameObject> graspingCandidatesMap = new Dictionary<RelocatableObjectInfo, GameObject>();

			for (int i=0; i<this.graspables.Count; i++)
			{
				RelocatableObjectInfo graspablePositionInfo = new RelocatableObjectInfo();

				graspablePositionInfo.name        = this.graspables[i].name;
				graspablePositionInfo.position    = graspingCandidatesPositionsTmp[i].transform.position - new Vector3(0, graspingCandidatesPositionsTmp[i].transform.localScale.y * 0.49f, 0);
				graspablePositionInfo.eulerAngles = graspingCandidatesPositionsTmp[i].transform.eulerAngles;

				graspingCandidatesMap.Add(graspablePositionInfo, this.graspables[i]);
			}

			return graspingCandidatesMap;
		}

		public string GetRoomName(GameObject roomObj)
		{
			if (roomObj == this.bedRoomArea) { return RoomNameForTaskMessageBedRoom; }
			if (roomObj == this.kitchenArea) { return RoomNameForTaskMessageKitchen; }
			if (roomObj == this.livingArea)  { return RoomNameForTaskMessageLiving; }
			if (roomObj == this.lobbyArea)   { return RoomNameForTaskMessageLobby; }

			throw new Exception("There is no grasping target in the 4 rooms. Grasping target =");
		}

		public string GenerateTaskMessage()
		{
			return "Go to the " + this.GetRoomName(this.targetRoom) + ", grasp the " + this.graspingTarget.name + " and come back here.";
		}


		public GameObject GetTargetRoom()
		{
			if (this.IsTargetInArea(this.graspingTarget.transform.position, this.bedRoomArea))
			{
				return this.bedRoomArea;
			}
			else if (this.IsTargetInArea(this.graspingTarget.transform.position, this.kitchenArea))
			{
				return this.kitchenArea;
			}
			else if (this.IsTargetInArea(this.graspingTarget.transform.position, this.livingArea))
			{
				return this.livingArea;
			}
			else if (this.IsTargetInArea(this.graspingTarget.transform.position, this.lobbyArea))
			{
				return this.lobbyArea;
			}
			else
			{
				throw new Exception("There is no grasping target in the 4 rooms. Grasping target = " 
					+ this.graspingTarget.name + ", method name=(" + System.Reflection.MethodBase.GetCurrentMethod().Name + ")");
			}
		}


		public bool IsRoomReachedSucceeded()
		{
			return this.IsTargetInArea(this.hsrBaseFootPrint.position, this.targetRoom, 0.215f);
		}


		public bool IsTargetInArea(Vector3 targetPosition, GameObject area, float margin)
		{
			Vector3 roomPosition = area.transform.position;

			BoxCollider boxCollider = area.GetComponent<BoxCollider>();

			float xMin = roomPosition.x + boxCollider.center.x - boxCollider.size.x / 2.0f + margin;
			float xMax = roomPosition.x + boxCollider.center.x + boxCollider.size.x / 2.0f - margin;
			float zMin = roomPosition.z + boxCollider.center.z - boxCollider.size.z / 2.0f + margin;
			float zMax = roomPosition.z + boxCollider.center.z + boxCollider.size.z / 2.0f - margin;

			if (xMin < targetPosition.x & targetPosition.x < xMax && zMin < targetPosition.z & targetPosition.z < zMax)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool IsTargetInArea(Vector3 targetPosition, GameObject area)
		{
			return IsTargetInArea(targetPosition, area, 0.0f);
		}


		public bool IsObjectGraspedSucceeded()
		{
			if (this.hsrGraspingDetector.GetGraspedObject() != null)
			{
				return this.graspingTarget == this.hsrGraspingDetector.GetGraspedObject();
			}

			return false;
		}

		public bool IsTaskFinishedSucceeded(Vector3 moderatorPosition)
		{
			return Vector3.Distance(this.hsrBaseFootPrint.position, moderatorPosition) <= 2.0f && this.IsObjectGraspedSucceeded();
		}


		public void InitializePlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				this.playbackRecorder.Initialize(HandymanConfig.Instance.numberOfTrials);
			}
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypePlay)
			{
				this.playbackPlayer.Initialize();
			}
		}


		public bool IsPlaybackInitialized()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsInitialized()) { return false; }
			}
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypePlay)
			{
				if(!this.playbackPlayer.IsInitialized()) { return false; }
			}

			return true;
		}


		public void StartPlayback()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStarted = this.playbackRecorder.Record();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the world playback recording"); }
			}
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypePlay)
			{
				bool isStarted = this.playbackPlayer.Play();

				if(!isStarted) { SIGVerseLogger.Warn("Cannot start the world playback playing"); }
			}
		}

		public void StopPlayback()
		{
			if (HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				bool isStopped = this.playbackRecorder.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the world playback recording"); }
			}
			if (HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypePlay)
			{
				bool isStopped = this.playbackPlayer.Stop();

				if(!isStopped) { SIGVerseLogger.Warn("Cannot stop the world playback playing"); }
			}
		}

		public bool IsPlaybackFinished()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
				if(!this.playbackRecorder.IsFinished()) { return false; }
			}

			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypePlay)
			{
				if(!this.playbackPlayer.IsFinished()) { return false; }
			}

			return true;
		}


		public EnvironmentInfo GetEnvironmentInfo()
		{
			string filePath = String.Format(Application.dataPath + EnvironmentInfoFileNameFormat, HandymanConfig.Instance.numberOfTrials);

			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			if (File.Exists(filePath))
			{
				// File open
				StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8);

				environmentInfo = JsonUtility.FromJson<EnvironmentInfo>(streamReader.ReadToEnd());

				streamReader.Close();
			}
			else
			{
				throw new Exception("Environment info file does not exist. filePath=" + filePath);
			}

			return environmentInfo;
		}

		public void SaveEnvironmentInfo(string environmentName, string graspingTargetName, Dictionary<RelocatableObjectInfo, GameObject> graspablesPositionMap)
		{
			EnvironmentInfo environmentInfo = new EnvironmentInfo();

			environmentInfo.environmentName    = environmentName;
			environmentInfo.graspingTargetName = graspingTargetName;

			List<RelocatableObjectInfo> graspablesPositions = new List<RelocatableObjectInfo>();

			foreach(KeyValuePair<RelocatableObjectInfo, GameObject> graspablePositionPair in graspablesPositionMap)
			{
				RelocatableObjectInfo graspableInfo = new RelocatableObjectInfo();
				graspableInfo.name        = graspablePositionPair.Value.name;
				graspableInfo.position    = graspablePositionPair.Key.position;
				graspableInfo.eulerAngles = graspablePositionPair.Key.eulerAngles;

				graspablesPositions.Add(graspableInfo);
			}

			environmentInfo.graspablesPositions = graspablesPositions;


			string filePath = String.Format(Application.dataPath + EnvironmentInfoFileNameFormat, HandymanConfig.Instance.numberOfTrials);

			StreamWriter streamWriter = new StreamWriter(filePath, false, Encoding.UTF8);

			SIGVerseLogger.Info("Save Environment info. path=" + filePath);

			streamWriter.WriteLine(JsonUtility.ToJson(environmentInfo, true));

			streamWriter.Flush();
			streamWriter.Close();
		}
	}
}

