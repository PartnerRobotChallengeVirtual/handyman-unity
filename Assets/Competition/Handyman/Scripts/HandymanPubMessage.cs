using UnityEngine.EventSystems;
using SIGVerse.Common;
using SIGVerse.RosBridge;

namespace SIGVerse.Competition.Handyman
{
	public interface IRosMsgSendHandler : IEventSystemHandler
	{
		void OnSendRosMessage(string message, string detail);
	}

	public class HandymanPubMessage : RosPubMessage<RosBridge.handyman.HandymanMsg>, IRosMsgSendHandler
	{
		public void OnSendRosMessage(string message, string detail)
		{
			SIGVerseLogger.Info("Sending message :" + message + ", " + detail);

			RosBridge.handyman.HandymanMsg handymanMsg = new RosBridge.handyman.HandymanMsg();
			handymanMsg.message = message;
			handymanMsg.detail = detail;

			this.publisher.Publish(handymanMsg);
		}
	}
}

