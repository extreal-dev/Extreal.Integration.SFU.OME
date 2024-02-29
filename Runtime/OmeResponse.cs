using System;
using System.Collections.Generic;
using UnityEngine;

namespace Extreal.Integration.SFU.OME
{
    [Serializable]
    public class GroupListResponse
    {
        public List<GroupResponse> Groups => groups;
        [SerializeField] private List<GroupResponse> groups;
    }

    [Serializable]
    public class GroupResponse
    {

        public string Name => name;
        [SerializeField] private string name;
    }
}
