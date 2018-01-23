using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using SIGVerse.Common;
using SIGVerse.ToyotaHSR;

namespace SIGVerse.Competition.Handyman
{
	public static class Score
	{
		public const int MaxScore = +999;
		public const int MinScore = -999;

		public enum Type
		{
			RoomReachedSuccess,
			ObjectGraspedSuccess,
			ComeBackSuccess,
			CollisionEnter,
		}

		public static int GetScore(Type scoreType)
		{
			switch(scoreType)
			{
				case Score.Type.RoomReachedSuccess  : { return +20; }
				case Score.Type.ObjectGraspedSuccess: { return +50; }
				case Score.Type.ComeBackSuccess     : { return +30; }
				case Score.Type.CollisionEnter      : { return -10; }
			}

			throw new Exception("Illegal score type. Type = " + (int)scoreType + ", method name=(" + System.Reflection.MethodBase.GetCurrentMethod().Name + ")");
		}
	}

	public class HandymanScoreManager : MonoBehaviour, IHSRCollisionHandler
	{
		private const string TimeFormat = "#####0";
		private const float DefaultTimeScale = 1.0f;

		public HandymanModerator moderator;

		[HeaderAttribute("Task status")]
		public Text challengeInfoText;
		public Text taskMessageText;

		//[HeaderAttribute("Buttons")]
		//public Button startButton;

		[HeaderAttribute("Time left")]
		[TooltipAttribute("seconds")]
		public int timeLimit = 600;

		public Text timeLeftValueText;

		[HeaderAttribute("Score")]
		public Text scoreValText;
		public Text totalValText;
		//---------------------------------------------------

		private float timeLeft;

		private int score;


		// Use this for initialization
		void Start()
		{
			this.timeLeft = (float)this.timeLimit;

			this.timeLeftValueText.text = this.timeLeft.ToString(TimeFormat);
			this.scoreValText.text = "0";
			this.totalValText.text = HandymanConfig.Instance.GetTotalScore().ToString();

			this.score = 0;

			Time.timeScale = 0.0f;
		}

		// Update is called once per frame
		void Update()
		{
			this.timeLeft = Mathf.Max(0.0f, this.timeLeft-Time.deltaTime);

			this.timeLeftValueText.text = this.timeLeft.ToString(TimeFormat);

			if(this.timeLeft == 0.0f)
			{
				this.moderator.InterruptTimeIsUp();
			}
		}


		public void AddScore(Score.Type scoreType)
		{
			this.score = Mathf.Clamp(this.score + Score.GetScore(scoreType), Score.MinScore, Score.MaxScore);

			this.scoreValText.text = this.score.ToString(TimeFormat);

			SIGVerseLogger.Info("Score add [" + Score.GetScore(scoreType) + "], Challenge " + HandymanConfig.Instance.numberOfTrials + " Score=" + this.score);
		}

		public void TaskStart()
		{
			this.scoreValText.text = this.score.ToString(TimeFormat);

			Time.timeScale = HandymanScoreManager.DefaultTimeScale;
		}

		public void TaskEnd()
		{
			Time.timeScale = 0.0f;

			HandymanConfig.Instance.AddScore(this.score);

			this.totalValText.text = HandymanConfig.Instance.GetTotalScore().ToString();

			SIGVerseLogger.Info("Total Score=" + this.totalValText.text);

			HandymanConfig.Instance.RecordScoreInFile();
		}

		public void ResetTimeLeftText()
		{
			this.timeLeft = (float)this.timeLimit;
			this.timeLeftValueText.text = this.timeLeft.ToString(TimeFormat);
		}

		public void SetTaskMessageText(string taskMessage)
		{
			this.taskMessageText.text = taskMessage;
		}

		public void SetChallengeInfoText()
		{
			int numberOfTrials = HandymanConfig.Instance.numberOfTrials;

			string ordinal;

			if (numberOfTrials == 11 || numberOfTrials == 12 || numberOfTrials == 13)
			{
				ordinal = "th";
			}
			else
			{
				if (numberOfTrials % 10 == 1)
				{
					ordinal = "st";
				}
				else if (numberOfTrials % 10 == 2)
				{
					ordinal = "nd";
				}
				else if (numberOfTrials % 10 == 3)
				{
					ordinal = "rd";
				}
				else
				{
					ordinal = "th";
				}
			}

			this.challengeInfoText.text = numberOfTrials + ordinal + " challenge";
		}

		public string GetChallengeInfoText()
		{
			return this.challengeInfoText.text;
		}

		public void OnHsrCollisionEnter()
		{
			this.AddScore(Score.Type.CollisionEnter);
		}
	}
}

