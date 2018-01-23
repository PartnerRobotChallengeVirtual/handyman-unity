using UnityEngine;


namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanPlaybackCommon))]
	public class HandymanPlaybackRecorder : WorldPlaybackRecorder
	{
		void Awake()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
			}
			else
			{
				this.enabled = false;
			}
		}

		// Use this for initialization
		void Start()
		{
			HandymanPlaybackCommon common = this.GetComponent<HandymanPlaybackCommon>();

			this.targetTransforms = common.GetTargetTransforms();
		}
	}
}
