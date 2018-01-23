using UnityEngine.EventSystems;
using SIGVerse.Common;

namespace SIGVerse.Competition.Handyman
{
	public interface IRosMsgSendHandler : IEventSystemHandler
	{
		void OnSendRosMessage(string message, string detail);
	}

	public class HandymanPubMessage : RosPubMessage<ROSBridge.handyman.HandymanMsg>, IRosMsgSendHandler
	{
		public void OnSendRosMessage(string message, string detail)
		{
			SIGVerseLogger.Info("Sending message :" + message + ", " + detail);

			ROSBridge.handyman.HandymanMsg handymanMsg = new ROSBridge.handyman.HandymanMsg();
			handymanMsg.message = message;
			handymanMsg.detail = detail;

			this.publisher.Publish(handymanMsg);
		}
	}
}

