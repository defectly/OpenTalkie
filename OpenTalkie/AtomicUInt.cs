namespace OpenTalkie;

using System.Threading;

public class AtomicUInt
{
    private uint value;

    public AtomicUInt(uint initialValue = 0)
    {
        value = initialValue;
    }

    public uint Increment()
    {
        uint initialValue;
        uint newValue;
        do
        {
            initialValue = value;
            newValue = initialValue + 1;
        }
        while (Interlocked.CompareExchange(ref value, newValue, initialValue) != initialValue);

        return newValue;
    }

    public static AtomicUInt operator ++(AtomicUInt atomicUInt)
    {
        atomicUInt.Increment();
        return atomicUInt;
    }

    public uint GetValue()
    {
        return value;
    }

    public void SetValue(uint newValue)
    {
        Interlocked.Exchange(ref value, newValue);
    }
}

