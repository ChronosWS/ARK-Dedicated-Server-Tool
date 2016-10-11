﻿using System;
using System.Windows;
using ARK_Server_Manager.Lib.ViewModel;

namespace ARK_Server_Manager.Lib
{
    public class EngramEntry : AggregateIniValue
    {
        public static readonly DependencyProperty ArkApplicationProperty = DependencyProperty.Register(nameof(ArkApplication), typeof(ArkApplication), typeof(EngramEntry), new PropertyMetadata(ArkApplication.SurvivalEvolved));
        public static readonly DependencyProperty EngramClassNameProperty = DependencyProperty.Register(nameof(EngramClassName), typeof(string), typeof(EngramEntry), new PropertyMetadata(String.Empty));
        public static readonly DependencyProperty EngramLevelRequirementProperty = DependencyProperty.Register(nameof(EngramLevelRequirement), typeof(int), typeof(EngramEntry), new PropertyMetadata(1));
        public static readonly DependencyProperty EngramPointsCostProperty = DependencyProperty.Register(nameof(EngramPointsCost), typeof(int), typeof(EngramEntry), new PropertyMetadata(1));
        public static readonly DependencyProperty EngramHiddenProperty = DependencyProperty.Register(nameof(EngramHidden), typeof(bool), typeof(EngramEntry), new PropertyMetadata(false));
        public static readonly DependencyProperty RemoveEngramPreReqProperty = DependencyProperty.Register(nameof(RemoveEngramPreReq), typeof(bool), typeof(EngramEntry), new PropertyMetadata(false));

        public ArkApplication ArkApplication
        {
            get { return (ArkApplication)GetValue(ArkApplicationProperty); }
            set { SetValue(ArkApplicationProperty, value); }
        }

        [AggregateIniValueEntry]
        public string EngramClassName
        {
            get { return (string)GetValue(EngramClassNameProperty); }
            set {
                SetValue(EngramClassNameProperty, value);
                DisplayName = EngramClassNameToDisplayNameConverter.Convert(value).ToString();
            }
        }

        [AggregateIniValueEntry]
        public int EngramLevelRequirement
        {
            get { return (int)GetValue(EngramLevelRequirementProperty); }
            set { SetValue(EngramLevelRequirementProperty, value); }
        }

        [AggregateIniValueEntry]
        public int EngramPointsCost
        {
            get { return (int)GetValue(EngramPointsCostProperty); }
            set { SetValue(EngramPointsCostProperty, value); }
        }

        [AggregateIniValueEntry]
        public bool EngramHidden
        {
            get { return (bool)GetValue(EngramHiddenProperty); }
            set { SetValue(EngramHiddenProperty, value); }
        }

        [AggregateIniValueEntry]
        public bool RemoveEngramPreReq
        {
            get { return (bool)GetValue(RemoveEngramPreReqProperty); }
            set { SetValue(RemoveEngramPreReqProperty, value); }
        }

        public string DisplayName
        {
            get;
            protected set;
        }

        public bool KnownEngram
        {
            get
            {
                return GameData.HasEngramForClass(EngramClassName);
            }
        }

        public static EngramEntry FromINIValue(string iniValue)
        {
            var newSpawn = new EngramEntry();
            newSpawn.InitializeFromINIValue(iniValue);
            return newSpawn;
        }

        public override bool IsEquivalent(AggregateIniValue other)
        {
            return String.Equals(this.EngramClassName, ((EngramEntry)other).EngramClassName, StringComparison.OrdinalIgnoreCase);
        }

        public override string GetSortKey()
        {
            return DisplayName;
        }

        public override bool ShouldSave()
        {
            if (!KnownEngram)
                return true;

            var engramEntry = GameData.GetEngramForClass(EngramClassName);
            if (engramEntry == null)
                return true;

            return (!engramEntry.EngramHidden.Equals(EngramHidden) ||
                !engramEntry.EngramPointsCost.Equals(EngramPointsCost) ||
                !engramEntry.EngramLevelRequirement.Equals(EngramLevelRequirement) ||
                !engramEntry.RemoveEngramPreReq.Equals(RemoveEngramPreReq));
        }

        protected override void InitializeFromINIValue(string value)
        {
            base.InitializeFromINIValue(value);

            if (!KnownEngram)
                ArkApplication = ArkApplication.Unknown;
        }
    }
}
