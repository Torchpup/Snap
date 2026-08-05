using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Snap.Engine.Inputs;

internal enum SdlBindingType
{
    Button,
    Axis,
    Hat
}

internal readonly struct SdlBinding
{
    public SdlBindingType Type { get; }
    public int Index { get; }
    public int HatMask { get; }

    public SdlBinding(SdlBindingType type, int index, int hatMask = 0)
    {
        Type = type;
        Index = index;
        HatMask = hatMask;
    }
}