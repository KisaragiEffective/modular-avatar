﻿using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Creates/allocates parameters to any Menu Items that need them.
    /// </summary>
    internal class ParameterAssignerPass : Pass<ParameterAssignerPass>
    {
        internal static bool ShouldAssignParametersToMami(ModularAvatarMenuItem item)
        {
            switch (item?.Control?.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    // ok
                    break;
                default:
                    return false;
            }
            
            foreach (var rc in item.GetComponentsInChildren<ReactiveComponent>(true))
            {
                // Only track components where this is the closest parent
                if (rc.transform == item.transform)
                {
                    return true;
                }
                
                var parentMami = rc.GetComponentInParent<ModularAvatarMenuItem>();
                if (parentMami == item)
                {
                    return true;
                }
            }
            
            return false;
        } 
        
        protected override void Execute(ndmf.BuildContext context)
        {
            var paramIndex = 0;

            var declaredParams = context.AvatarDescriptor.expressionParameters.parameters.Select(p => p.name)
                .ToHashSet();

            Dictionary<string, VRCExpressionParameters.Parameter> newParameters = new();

            foreach (var mami in context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                if (string.IsNullOrWhiteSpace(mami.Control?.parameter?.name))
                {
                    if (!ShouldAssignParametersToMami(mami)) continue;
                    
                    if (mami.Control == null) mami.Control = new VRCExpressionsMenu.Control();
                    mami.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = $"__MA/AutoParam/{mami.gameObject.name}${paramIndex++}"
                    };
                }

                var paramName = mami.Control.parameter.name;

                if (!declaredParams.Contains(paramName))
                {
                    newParameters.TryGetValue(paramName, out var existingNewParam);
                    var wantedType = existingNewParam?.valueType ?? VRCExpressionParameters.ValueType.Bool;

                    if (wantedType != VRCExpressionParameters.ValueType.Float &&
                        (mami.Control.value > 1.01 || mami.Control.value < -0.01))
                        wantedType = VRCExpressionParameters.ValueType.Int;

                    if (Mathf.Abs(Mathf.Round(mami.Control.value) - mami.Control.value) > 0.01f)
                        wantedType = VRCExpressionParameters.ValueType.Float;

                    if (existingNewParam == null)
                    {
                        existingNewParam = new VRCExpressionParameters.Parameter
                        {
                            name = paramName,
                            valueType = wantedType,
                            saved = mami.isSaved,
                            defaultValue = -1,
                            networkSynced = mami.isSynced
                        };
                        newParameters[paramName] = existingNewParam;
                    }
                    else
                    {
                        existingNewParam.valueType = wantedType;
                    }

                    // TODO: warn on inconsistent configuration
                    existingNewParam.saved = existingNewParam.saved || mami.isSaved;
                    existingNewParam.networkSynced = existingNewParam.networkSynced || mami.isSynced;
                    existingNewParam.defaultValue = mami.isDefault ? mami.Control.value : existingNewParam.defaultValue;
                }
            }

            if (newParameters.Count > 0)
            {
                foreach (var p in newParameters)
                    if (p.Value.defaultValue < 0)
                        p.Value.defaultValue = 0;

                var expParams = context.AvatarDescriptor.expressionParameters;
                if (!context.IsTemporaryAsset(expParams))
                {
                    expParams = Object.Instantiate(expParams);
                    context.AvatarDescriptor.expressionParameters = expParams;
                }

                expParams.parameters = expParams.parameters.Concat(newParameters.Values).ToArray();
            }
        }

        internal static ControlCondition AssignMenuItemParameter(ModularAvatarMenuItem mami, Dictionary<string, float> simulationInitialStates = null)
        {
            var paramName = mami?.Control?.parameter?.name;
            if (mami?.Control != null && simulationInitialStates != null && ShouldAssignParametersToMami(mami))
            {
                paramName = "___AutoProp/" + mami.Control?.parameter?.name;

                if (mami.isDefault)
                {
                    simulationInitialStates[paramName] = mami.Control.value;
                } else if (!simulationInitialStates.ContainsKey(paramName))
                {
                    simulationInitialStates[paramName] = -999;
                }
            }
            
            if (string.IsNullOrWhiteSpace(paramName)) return null;
            
            return new ControlCondition
            {
                Parameter = paramName,
                DebugName = mami.gameObject.name,
                IsConstant = false,
                InitialValue = mami.isDefault ? mami.Control.value : -999, // TODO
                ParameterValueLo = mami.Control.value - 0.5f,
                ParameterValueHi = mami.Control.value + 0.5f,
                DebugReference = mami,
            };
        }
    }
}