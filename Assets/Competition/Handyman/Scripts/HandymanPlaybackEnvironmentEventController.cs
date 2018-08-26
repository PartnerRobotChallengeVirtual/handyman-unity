using UnityEngine;
using System;
using System.Collections.Generic;
using SIGVerse.Common;

namespace SIGVerse.Competition.Handyman
{
	public class PlaybackEnvironmentEvent : PlaybackEventBase
	{
		public List<GameObject> Environments    { get; set; }
		public String           EnvironmentName { get; set; }

		public void Execute()
		{
			// Activate/Deactivate the environments
			foreach(GameObject environment in this.Environments)
			{
				if(environment.name == this.EnvironmentName)
				{
					environment.SetActive(true);
				}
				else
				{
					environment.SetActive(false);
				}
			}
		}
	}


	public class PlaybackEnvironmentEventList : PlaybackEventListBase<PlaybackEnvironmentEvent>
	{
		public PlaybackEnvironmentEventList()
		{
			this.EventList = new List<PlaybackEnvironmentEvent>();
		}
	}


	// ------------------------------------------------------------------

	public class HandymanPlaybackEnvironmentEventController : PlaybackEventControllerBase<PlaybackEnvironmentEventList, PlaybackEnvironmentEvent>
	{
		private const string DataType1 = HandymanPlaybackCommon.DataType1EnvironmentInfo;

		private List<GameObject> environments;

		public HandymanPlaybackEnvironmentEventController(List<GameObject> environments)
		{
			this.environments = environments;
		}

		public override void StartInitializingEvents()
		{
			this.eventLists = new List<PlaybackEnvironmentEventList>();
		}

		public override bool ReadEvents(string[] headerArray, string dataStr)
		{
			// Environment Name
			if (headerArray[1] == DataType1)
			{
//				string[] dataArray = dataStr.Split('\t');

				PlaybackEnvironmentEventList environmentEventList = new PlaybackEnvironmentEventList();
				environmentEventList.ElapsedTime = float.Parse(headerArray[0]);

				PlaybackEnvironmentEvent environmentEvent = new PlaybackEnvironmentEvent();
				environmentEvent.Environments    = this.environments;
				environmentEvent.EnvironmentName = dataStr;

				environmentEventList.EventList.Add(environmentEvent);

				this.eventLists.Add(environmentEventList);

				return true;
			}

			return false;
		}


		public static string GetDefinitionLine(string environmentName)
		{
			return "0.0," + DataType1 + "\t" + environmentName;
		}
	}
}

