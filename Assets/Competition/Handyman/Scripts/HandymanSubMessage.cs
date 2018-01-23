using UnityEngine;
using UnityEngine.EventSystems;
using SIGVerse.Common;

namespace SIGVerse.Competition.Handyman
{
	public interface IRosMsgReceiveHandler : IEventSystemHandler
	{
		void OnReceiveRosMessage(ROSBridge.handyman.HandymanMsg handymanMsg);
	}

	public class HandymanSubMessage : RosSubMessage<ROSBridge.handyman.HandymanMsg>
	{
		override public void SubscribeMessageCallback(ROSBridge.handyman.HandymanMsg handymanMsg)
		{
			SIGVerseLogger.Info("Received message :"+handymanMsg.message);

			foreach(GameObject destination in this.destinations)
			{
				ExecuteEvents.Execute<IRosMsgReceiveHandler>
				(
					target: destination,
					eventData: null,
					functor: (reciever, eventData) => reciever.OnReceiveRosMessage(handymanMsg)
				);
			}
		}
	}
}
