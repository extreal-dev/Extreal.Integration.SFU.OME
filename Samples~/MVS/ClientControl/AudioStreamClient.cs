using Extreal.Core.Common.System;

namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public abstract class AudioStreamClient : DisposableBase
    {
        protected sealed override void ReleaseManagedResources() => DoReleaseManagedResources();
        protected virtual void DoReleaseManagedResources() { }
    }
}
