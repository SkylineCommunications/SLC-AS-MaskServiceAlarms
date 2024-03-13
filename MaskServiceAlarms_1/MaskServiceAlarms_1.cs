/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

13/03/2024	1.0.0.1		HAN	            Initial version
****************************************************************************
*/

namespace MaskAlarms_1
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
    using Skyline.DataMiner.Net.Helper;
    using Skyline.DataMiner.Net.Messages;
	using AlarmLevel = Skyline.DataMiner.Core.DataMinerSystem.Common.AlarmLevel;
	using ElementState = Skyline.DataMiner.Core.DataMinerSystem.Common.ElementState;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
	public class Script
    {
        public enum Action
        {
            Mask = 8,
            Unmask = 9,
        }

        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(IEngine engine)
        {
            IDms thisDms = engine.GetDms();

            string viewName = engine.GetScriptParam("View").Value;
            string action = engine.GetScriptParam("Action").Value;

            Service[] services = engine.FindServicesInView(viewName);

            if (services.IsNullOrEmpty())
            {
                engine.GenerateInformation("View does not contain any services");
                return;
            }

            foreach (var service in services)
            {
                foreach (var children in service.RawInfo.Children)
                {
                    if (action.Equals("Mask"))
                    {
                        MaskElementsInService(engine, thisDms, children);
                    }
                    else if(action.Equals("Unmask"))
                    {
                        UnmaskElementsInService(engine, thisDms, children);
                    }
                    else
                    {
                        engine.GenerateInformation("Action not supported");
                    }
                }
            }
        }

        private static void MaskElementsInService(IEngine engine, IDms thisDms, LiteServiceChildInfo children)
        {
            var element = engine.FindElement(children.DataMinerID, children.ElementID);
            var dmsElement = thisDms.GetElement(element.ElementName);
            var alarmLevel = dmsElement.GetAlarmLevel();
            var elementState = dmsElement.State;

            if (elementState != ElementState.Masked)
            {
                if (alarmLevel == AlarmLevel.Timeout)
                {
                    element.Mask("Mask");
                }
                else if (dmsElement.GetAlarmLevel() == AlarmLevel.Critical)
                {
                    ActiveAlarmsResponseMessage getAlarmsResponse = GetActiveAlarms(engine, element);

                    foreach (var activeAlarm in getAlarmsResponse.ActiveAlarms)
                    {
                        AlarmAction(engine, element, activeAlarm, Convert.ToInt32(Action.Mask));
                    }
                }
                else
                {
                    // No action needed
                }
            }
        }

        private static void UnmaskElementsInService(IEngine engine, IDms thisDms, LiteServiceChildInfo children)
        {
            var element = engine.FindElement(children.DataMinerID, children.ElementID);
            var dmsElement = thisDms.GetElement(element.ElementName);
            var alarmLevel = dmsElement.GetAlarmLevel();

            if (alarmLevel == AlarmLevel.Masked)
            {
                element.Unmask();
            }
            else if (alarmLevel == AlarmLevel.Normal)
            {
                ActiveAlarmsResponseMessage getAlarmsResponse = GetActiveAlarms(engine, element);

                if (getAlarmsResponse.Equals("0 Active Alarms"))
                {
                    engine.GenerateInformation("There are no Alarms to unmask");
                    return;
                }

                foreach (var activeAlarm in getAlarmsResponse.ActiveAlarms)
                {
                    AlarmAction(engine, element, activeAlarm, Convert.ToInt32(Action.Unmask));
                }
            }
            else
            {
                // No action needed
            }
        }

        private static ActiveAlarmsResponseMessage GetActiveAlarms(IEngine engine, Element element)
        {
            var getAlarmsMessage = new GetActiveAlarmsMessage
            {
                DataMinerID = element.DmaId,
                ElementID = element.ElementId,
                HostingDataMinerID = -1,
                ParameterID = -1,
            };

            ActiveAlarmsResponseMessage getAlarmsResponse = engine.SendSLNetSingleResponseMessage(getAlarmsMessage) as ActiveAlarmsResponseMessage;
            return getAlarmsResponse;
        }

        private static void AlarmAction(IEngine engine, Element element, AlarmEventMessage activeAlarm, int action)
        {
            SetAlarmStateMessage maskAlarm = new SetAlarmStateMessage
            {
                AlarmId = activeAlarm.AlarmID,
                DataMinerID = element.DmaId,
                ElementID = element.ElementId,
                State = action,
            };

            engine.SendSLNetMessage(maskAlarm);
        }
    }
}