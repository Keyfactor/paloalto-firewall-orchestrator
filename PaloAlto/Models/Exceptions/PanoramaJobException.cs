using System;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Exceptions;

public class PanoramaJobException : Exception
{
    public PanoramaJobException(string message) : base(message)
    {
    }
}
