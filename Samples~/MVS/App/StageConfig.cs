using Extreal.Core.StageNavigation;
using UnityEngine;

namespace Extreal.Integration.SFU.OME.MVS.App
{
    [CreateAssetMenu(
        menuName = "SFU.OME.MVS/" + nameof(StageConfig),
        fileName = nameof(StageConfig))]
    public class StageConfig : StageConfigBase<StageName, SceneName>
    {
    }
}
