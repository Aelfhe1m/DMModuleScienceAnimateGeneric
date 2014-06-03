﻿/* DMagic Orbital Science - Module Science Animate Generic
 * Generic module for animated science experiments.
 *
 * Copyright (c) 2014, David Grandy
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, 
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice, 
 * this list of conditions and the following disclaimer in the documentation and/or other materials 
 * provided with the distribution.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *  
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;

namespace DMModuleScienceAnimateGeneric
{
    public class DMModuleScienceAnimateGeneric : ModuleScienceExperiment, IScienceDataContainer
    {
        [KSPField]
        public string customFailMessage = null;
        [KSPField]
        public string deployingMessage = null;
        [KSPField]
        public string planetFailMessage = null;
        [KSPField(isPersistant = true)]
        public bool IsDeployed;
        [KSPField]
        public string animationName = null;
        //[KSPField]
        //public bool allowManualControl = false;
        [KSPField(isPersistant = false)]
        public float animSpeed = 1f;
        //[KSPField(isPersistant = true)]
        //public bool animSwitch = true;
        //[KSPField(isPersistant = true)]
        //public float animTime = 0f;
        [KSPField]
        public string endEventGUIName = "Retract";
        [KSPField]
        public bool showEndEvent = true;
        [KSPField]
        public string startEventGUIName = "Deploy";
        [KSPField]
        public bool showStartEvent = true;
        [KSPField]
        public string toggleEventGUIName = "Toggle";
        [KSPField]
        public bool showToggleEvent = false;
        [KSPField]
        public bool showEditorEvents = true;

        [KSPField]
        public bool experimentAnimation = true;
        [KSPField]
        public bool experimentWaitForAnimation = false;
        [KSPField]
        public float waitForAnimationTime = -1;
        [KSPField]
        public int keepDeployedMode = 0;
        [KSPField]
        public bool oneWayAnimation = false;
        [KSPField]
        public string resourceExperiment = "ElectricCharge";
        [KSPField]
        public float resourceExpCost = 0;
        [KSPField]
        public bool asteroidReports = false;
        [KSPField]
        public int planetaryMask = 524287;

        protected Animation anim;
        protected ScienceExperiment scienceExp;
        private DMAsteroidScienceGen newAsteroid = null;
        private bool resourceOn = false;
        private int dataIndex = 0;

        //Record some default values for Eeloo here to prevent the asteroid science method from screwing them up
        private const string bodyNameFixed = "Eeloo";
                
        List<ScienceData> scienceReportList = new List<ScienceData>();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            this.part.force_activate();
            if (part.FindModelAnimators(animationName).Length > 0 && !string.IsNullOrEmpty(animationName))
            {
                anim = part.FindModelAnimators(animationName).First();
            }
            if (state == StartState.Editor) editorSetup();
            else
            {
                setup();
                if (IsDeployed) primaryAnimator(1f, 1f, WrapMode.Default);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.RemoveNodes("ScienceData");
            foreach (ScienceData storedData in scienceReportList)
            {
                ConfigNode storedDataNode = node.AddNode("ScienceData");
                storedData.Save(storedDataNode);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("ScienceData"))
            {
                foreach (ConfigNode storedDataNode in node.GetNodes("ScienceData"))
                {
                    ScienceData data = new ScienceData(storedDataNode);
                    scienceReportList.Add(data);
                }
            }
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
            eventsCheck();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (resourceOn)
            {
                if (PartResourceLibrary.Instance.GetDefinition(resourceExperiment) != null)
                {
                    float cost = resourceExpCost * TimeWarp.deltaTime;
                    if (part.RequestResource(resourceExperiment, cost) < cost)
                    {
                        StopCoroutine("WaitForAnimation");
                        resourceOn = false;
                        ScreenMessages.PostScreenMessage("Not enough " + resourceExperiment + ", shutting down experiment", 4f, ScreenMessageStyle.UPPER_CENTER);
                        if (keepDeployedMode == 0 || keepDeployedMode == 1) retractEvent();
                    }
                }
            }
        }

        public override string GetInfo()
        {
            if (resourceExpCost > 0)
            {
                string info = base.GetInfo();
                info += ".\nRequires:\n-" + resourceExperiment + ": " + resourceExpCost.ToString() + "/s for " + waitForAnimationTime.ToString() + "s\n";
                return info;
            }
            else return base.GetInfo();
        }

        public void setup()
        {
            Events["deployEvent"].guiActive = showStartEvent;
            Events["retractEvent"].guiActive = showEndEvent;
            Events["toggleEvent"].guiActive = showToggleEvent;
            Events["deployEvent"].guiName = startEventGUIName;
            Events["retractEvent"].guiName = endEventGUIName;
            Events["toggleEvent"].guiName = toggleEventGUIName;
            if (waitForAnimationTime == -1) waitForAnimationTime = anim[animationName].length / animSpeed;
            if (experimentID != null) scienceExp = ResearchAndDevelopment.GetExperiment(experimentID);
            if (FlightGlobals.Bodies[16].bodyName != "Eeloo") FlightGlobals.Bodies[16].bodyName = bodyNameFixed;
        }

        public void editorSetup()
        {
            Actions["deployAction"].active = showStartEvent;
            Actions["retractAction"].active = showEndEvent;
            Actions["toggleAction"].active = showToggleEvent;
            Actions["deployAction"].guiName = startEventGUIName;
            Actions["retractAction"].guiName = endEventGUIName;
            Actions["toggleAction"].guiName = toggleEventGUIName;
            Events["editorDeployEvent"].guiName = startEventGUIName;
            Events["editorRetractEvent"].guiName = endEventGUIName;
            Events["editorDeployEvent"].active = showEditorEvents;
            Events["editorRetractEvent"].active = false;
        }

        #region Animators

        public void primaryAnimator(float speed, float time, WrapMode wrap)
        {
            if (anim != null)
            {
                anim[animationName].speed = speed;
                if (!anim.IsPlaying(animationName))
                {
                    anim[animationName].wrapMode = wrap;
                    anim[animationName].normalizedTime = time;
                    anim.Play(animationName);
                }
            }
        }

        [KSPEvent(guiActive = true, guiName = "Deploy", active = true)]
        public void deployEvent()
        {
            primaryAnimator(animSpeed * 1f, 0f, WrapMode.Default);
            IsDeployed = !oneWayAnimation;
            Events["deployEvent"].active = oneWayAnimation;
            Events["retractEvent"].active = showEndEvent;
        }

        [KSPAction("Deploy")]
        public void deployAction(KSPActionParam param)
        {
            deployEvent();
        }

        [KSPEvent(guiActive = true, guiName = "Retract", active = false)]
        public void retractEvent()
        {
            if (oneWayAnimation) return;
            primaryAnimator(-1f * animSpeed, 1f, WrapMode.Default);
            IsDeployed = false;
            Events["deployEvent"].active = showStartEvent;
            Events["retractEvent"].active = false;
        }

        [KSPAction("Retract")]
        public void retractAction(KSPActionParam param)
        {
            retractEvent();
        }

        [KSPEvent(guiActive = true, guiName = "Toggle", active = true)]
        public void toggleEvent()
        {
            if (IsDeployed) retractEvent();
            else deployEvent();
        }

        [KSPAction("Toggle")]
        public void toggleAction(KSPActionParam Param)
        {
            toggleEvent();
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Deploy", active = true)]
        public void editorDeployEvent()
        {
            deployEvent();
            IsDeployed = false;
            Events["editorDeployEvent"].active = oneWayAnimation;
            Events["editorRetractEvent"].active = !oneWayAnimation;
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Retract", active = false)]
        public void editorRetractEvent()
        {
            retractEvent();
            Events["editorDeployEvent"].active = true;
            Events["editorRetractEvent"].active = false;
        }

        #endregion

        #region Science Events and Actions

        new public void ResetExperiment()
        {
            if (scienceReportList.Count > 0)
            {
                if (keepDeployedMode == 0) retractEvent();
                scienceReportList.Clear();
            }
            eventsCheck();
        }

        new public void ResetAction(KSPActionParam param)
        {
            ResetExperiment();
        }

        new public void CollectDataExternalEvent()
        {   
            List<ModuleScienceContainer> EVACont = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();
            if (scienceReportList.Count > 0)
            {
                if (EVACont.First().StoreData(new List<IScienceDataContainer> { this }, false)) DumpAllData(scienceReportList);
            }
        }
        
        new public void ResetExperimentExternal()
        {
            ResetExperiment();
        }

        public void eventsCheck()
        {
            Events["ResetExperiment"].active = scienceReportList.Count > 0;
            Events["ResetExperimentExternal"].active = scienceReportList.Count > 0;
            Events["CollectDataExternalEvent"].active = scienceReportList.Count > 0;
            Events["DeployExperiment"].active = !Inoperable;
            Events["ReviewDataEvent"].active = scienceReportList.Count > 0;
        }

        #endregion

        #region Science Experiment Setup

        //Can't use base.DeployExperiment here, we need to create our own science data and control the experiment results page
        new public void DeployExperiment()
        {
            if (Inoperable) ScreenMessages.PostScreenMessage("Experiment is no longer functional; must be reset at a science lab or returned to Kerbin", 6f, ScreenMessageStyle.UPPER_CENTER);
            else if (scienceReportList.Count == 0)
            {
                if (canConduct())
                {
                    if (experimentAnimation)
                    {
                        if (anim.IsPlaying(animationName)) return;
                        else
                        {
                            if (!IsDeployed)
                            {
                                deployEvent();
                                if (!string.IsNullOrEmpty(deployingMessage)) ScreenMessages.PostScreenMessage(deployingMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
                                if (experimentWaitForAnimation)
                                {
                                    if (resourceExpCost > 0) resourceOn = true;
                                    StartCoroutine("WaitForAnimation", waitForAnimationTime);
                                }
                                else runExperiment();
                            }
                            else if (resourceExpCost > 0)
                            {
                                resourceOn = true;
                                StartCoroutine("WaitForAnimation", waitForAnimationTime);
                            }
                            else runExperiment();
                        }
                    }
                    else if (resourceExpCost > 0)
                    {
                        if (!string.IsNullOrEmpty(deployingMessage)) ScreenMessages.PostScreenMessage(deployingMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
                        resourceOn = true;
                        StartCoroutine("WaitForAnimation", waitForAnimationTime);
                    }
                    else runExperiment();
                }
            }
            else eventsCheck();
        }

        new public void DeployAction(KSPActionParam param)
        {
            DeployExperiment();
        }

        //In case we need to wait for an animation to finish before running the experiment
        public IEnumerator WaitForAnimation(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            resourceOn = false;
            runExperiment();
        }

        public void runExperiment()
        {
            ScienceData data = makeScience();
            scienceReportList.Add(data);
            dataIndex = scienceReportList.Count - 1;
            if (data != null) ReviewData();
            if (keepDeployedMode == 1) retractEvent();
        }
        
        //Create the science data
        public ScienceData makeScience()
        {
            ExperimentSituations vesselSituation = getSituation();
            string biome = getBiome(vesselSituation);
            CelestialBody mainBody = vessel.mainBody;
            bool asteroid = false;

            //Check for asteroids and alter the biome and celestialbody values as necessary
            if (asteroidReports && DMAsteroidScienceGen.asteroidGrappled() || asteroidReports && DMAsteroidScienceGen.asteroidNear())
            {
                newAsteroid = new DMAsteroidScienceGen();
                mainBody = newAsteroid.body;
                biome = newAsteroid.aClass;    
                asteroid = true;            
            }

            ScienceData data = null;
            ScienceExperiment exp = ResearchAndDevelopment.GetExperiment(experimentID);
            ScienceSubject sub = ResearchAndDevelopment.GetExperimentSubject(exp, vesselSituation, mainBody, biome);
            sub.title = exp.experimentTitle + situationCleanup(vesselSituation, biome);

            if (asteroid)
            {
                sub.subjectValue = newAsteroid.sciMult;
                sub.scienceCap = exp.scienceCap * sub.subjectValue;
                mainBody.bodyName = bodyNameFixed;
                asteroid = false;
            }
            else
            {
                sub.subjectValue = fixSubjectValue(vesselSituation, mainBody, sub.subjectValue);
                sub.scienceCap = exp.scienceCap * sub.subjectValue;
            }

            if (sub != null) data = new ScienceData(exp.baseValue * sub.dataScale, xmitDataScalar, 0.5f, sub.id, sub.title);
            return data;
        }

        private float fixSubjectValue(ExperimentSituations s, CelestialBody b, float f)
        {
            float subV = f;
            if (s == ExperimentSituations.SrfLanded) subV = b.scienceValues.LandedDataValue;
            else if (s == ExperimentSituations.SrfSplashed) subV = b.scienceValues.SplashedDataValue;
            else if (s == ExperimentSituations.FlyingLow) subV = b.scienceValues.FlyingLowDataValue;
            else if (s == ExperimentSituations.FlyingHigh) subV = b.scienceValues.FlyingHighDataValue;
            else if (s == ExperimentSituations.InSpaceLow) subV = b.scienceValues.InSpaceLowDataValue;
            else if (s == ExperimentSituations.InSpaceHigh) subV = b.scienceValues.InSpaceHighDataValue;
            return subV;
        }
        
        public string getBiome(ExperimentSituations s)
        {
            if (scienceExp.BiomeIsRelevantWhile(s))
            {
                switch (vessel.landedAt)
                {
                    case "LaunchPad":
                        return vessel.landedAt;
                    case "Runway":
                        return vessel.landedAt;
                    case "KSC":
                        return vessel.landedAt;
                    default:
                        return FlightGlobals.currentMainBody.BiomeMap.GetAtt(vessel.latitude * Mathf.Deg2Rad, vessel.longitude * Mathf.Deg2Rad).name;
                }
            }
            else return "";
        }

        public bool canConduct()
        {
            if (!planetaryScienceIndex.planetConfirm(planetaryMask, asteroidReports))
            {
                if (!string.IsNullOrEmpty(planetFailMessage)) ScreenMessages.PostScreenMessage(planetFailMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            if (!scienceExp.IsAvailableWhile(getSituation(), vessel.mainBody))
            {
                if (!string.IsNullOrEmpty(customFailMessage)) ScreenMessages.PostScreenMessage(customFailMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            return true;
        }

        //Get our experimental situation based on the vessel's current flight situation, fix stock bugs with aerobraking and reentry.
        public ExperimentSituations getSituation()
        {
            //Check for asteroids, return values that should sync with existing parts
            if (asteroidReports && DMAsteroidScienceGen.asteroidGrappled()) return ExperimentSituations.SrfLanded;
            if (asteroidReports && DMAsteroidScienceGen.asteroidNear()) return ExperimentSituations.InSpaceLow;
            switch (vessel.situation)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.PRELAUNCH:
                    return ExperimentSituations.SrfLanded;
                case Vessel.Situations.SPLASHED:
                    return ExperimentSituations.SrfSplashed;
                default:
                    if (vessel.altitude < (vessel.mainBody.atmosphereScaleHeight * 1000 * Math.Log(1e6)) && vessel.mainBody.atmosphere)
                    {
                        if (vessel.altitude < vessel.mainBody.scienceValues.flyingAltitudeThreshold)
                            return ExperimentSituations.FlyingLow;
                        else
                            return ExperimentSituations.FlyingHigh;
                    }
                    if (vessel.altitude < vessel.mainBody.scienceValues.spaceAltitudeThreshold)
                        return ExperimentSituations.InSpaceLow;
                    else
                        return ExperimentSituations.InSpaceHigh;
            }
        }

        //This is for the title bar of the experiment results page
        public string situationCleanup(ExperimentSituations expSit, string b)
        {
            //Add some asteroid specefic results
            if (asteroidReports && DMAsteroidScienceGen.asteroidGrappled()) return " from the surface of a " + b + " asteroid";
            if (asteroidReports && DMAsteroidScienceGen.asteroidNear()) return " while in space near a " + b + " asteroid";
            if (vessel.landedAt != "") return " from " + b;
            if (b == "")
            {
                switch (expSit)
                {
                    case ExperimentSituations.SrfLanded:
                        return " from  " + vessel.mainBody.theName + "'s surface";
                    case ExperimentSituations.SrfSplashed:
                        return " from " + vessel.mainBody.theName + "'s oceans";
                    case ExperimentSituations.FlyingLow:
                        return " while flying at " + vessel.mainBody.theName;
                    case ExperimentSituations.FlyingHigh:
                        return " from " + vessel.mainBody.theName + "'s upper atmosphere";
                    case ExperimentSituations.InSpaceLow:
                        return " while in space near " + vessel.mainBody.theName;
                    default:
                        return " while in space high over " + vessel.mainBody.theName;
                }
            }
            else
            {
                switch (expSit)
                {
                    case ExperimentSituations.SrfLanded:
                        return " from " + vessel.mainBody.theName + "'s " + b;
                    case ExperimentSituations.SrfSplashed:
                        return " from " + vessel.mainBody.theName + "'s " + b;
                    case ExperimentSituations.FlyingLow:
                        return " while flying over " + vessel.mainBody.theName + "'s " + b;
                    case ExperimentSituations.FlyingHigh:
                        return " from the upper atmosphere over " + vessel.mainBody.theName + "'s " + b;
                    case ExperimentSituations.InSpaceLow:
                        return " from space just above " + vessel.mainBody.theName + "'s " + b;
                    default:
                        return " while in space high over " + vessel.mainBody.theName + "'s " + b;
                }
            }
        }

        //Custom experiment results dialog page, allows full control over the buttons on that page
        public void newResultPage()
        {
            if (scienceReportList.Count > 0)
            {
                ScienceData data = scienceReportList[dataIndex];
                ExperimentResultDialogPage page = new ExperimentResultDialogPage(part, data, data.transmitValue, xmitDataScalar / 2, !rerunnable, transmitWarningText, true, data.labBoost < 1 && checkLabOps() && xmitDataScalar < 1, new Callback<ScienceData>(onDiscardData), new Callback<ScienceData>(onKeepData), new Callback<ScienceData>(onTransmitData), new Callback<ScienceData>(onSendToLab));
                ExperimentsResultDialog.DisplayResult(page);
            }
            eventsCheck();
        }

        new public void ReviewData()
        {
            dataIndex = 0;
            foreach (ScienceData data in scienceReportList)
            {
                newResultPage();
                dataIndex++;
            }
        }

        new public void ReviewDataEvent()
        {
            ReviewData();
        }

        #endregion   

        #region IScienceDataContainer methods
        
        //Implement these interface methods to make the science lab and transmitters function properly.
        ScienceData[] IScienceDataContainer.GetData()
        {
            return scienceReportList.ToArray();
        }

        int IScienceDataContainer.GetScienceCount()
        {
            return scienceReportList.Count;
        }

        bool IScienceDataContainer.IsRerunnable()
        {
            return base.IsRerunnable();
        }

        void IScienceDataContainer.ReviewData()
        {
            ReviewData();
        }

        void IScienceDataContainer.ReviewDataItem(ScienceData data)
        {
            ReviewData();
        }

        //Still not quite sure what exactly this is doing
        new public ScienceData[] GetData()
        {
            return scienceReportList.ToArray();
        }

        new public bool IsRerunnable()
        {
            return base.IsRerunnable();
        }

        new public int GetScienceCount()
        {
            return scienceReportList.Count;
        }

        //This is called after data is transmitted by right-clicking on the transmitter itself, removes all reports.
        void IScienceDataContainer.DumpData(ScienceData data)
        {
            if (scienceReportList.Count > 0)
            {
                base.DumpData(data);
                if (keepDeployedMode == 0) retractEvent();
                scienceReportList.Clear();
            }
            eventsCheck();
        }

        //This one is called after external data collection, removes all science reports.
        public void DumpAllData(List<ScienceData> dataList)
        {
            if (scienceReportList.Count > 0)
            {
                foreach (ScienceData data in dataList)
                {
                    base.DumpData(data);
                }
                scienceReportList.Clear();
                if (keepDeployedMode == 0) retractEvent();
            }
            eventsCheck();
        }

        //This one is called from the results page, removes only one report.
        new public void DumpData(ScienceData data)
        {
            if (scienceReportList.Count > 0)
            {
                base.DumpData(data);
                if (keepDeployedMode == 0) retractEvent();
                scienceReportList.Remove(data);
            }
            eventsCheck();
        }

        #endregion

        #region Experiment Results Control

        private void onDiscardData(ScienceData data)
        {
            if (scienceReportList.Count > 0)
            {
                scienceReportList.Remove(data);
                if (keepDeployedMode == 0) retractEvent();
            }
            eventsCheck();
        }

        private void onKeepData(ScienceData data)
        {
        }
        
        private void onTransmitData(ScienceData data)
        {
            List<IScienceDataTransmitter> tranList = vessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (tranList.Count > 0 && scienceReportList.Count > 0)
            {
                tranList.OrderBy(ScienceUtil.GetTransmitterScore).First().TransmitData(new List<ScienceData> {data});
                DumpData(data);
            }
            else ScreenMessages.PostScreenMessage("No transmitters available on this vessel.", 4f, ScreenMessageStyle.UPPER_LEFT);
        }

        private void onSendToLab(ScienceData data)
        {
            List<ModuleScienceLab> labList = vessel.FindPartModulesImplementing<ModuleScienceLab>();
            if (checkLabOps() && scienceReportList.Count > 0) labList.OrderBy(ScienceUtil.GetLabScore).First().StartCoroutine(labList.First().ProcessData(data, new Callback<ScienceData>(onComplete)));
            else ScreenMessages.PostScreenMessage("No operational lab modules on this vessel. Cannot analyze data.", 4f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void onComplete(ScienceData data)
        {
            ReviewData();
        }

        //Maybe unnecessary, can be folded into a simpler method???
        public bool checkLabOps()
        {
            List<ModuleScienceLab> labList = vessel.FindPartModulesImplementing<ModuleScienceLab>();
            bool labOp = false;
            for (int i = 0; i < labList.Count; i++)
            {
                if (labList[i].IsOperational())
                {
                    labOp = true;
                    break;
                }
            }
            return labOp;
        }

        #endregion

    }
}
