namespace NpcForge.Api;

// Faults are ASP.NET Core's problem details, and the page reads one error type for every one of them
// (ADR-019, decision 4). The routes and the forge answer through here, so a fault has one shape.
internal static class Problems
{
    public static IResult Of(int status, string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: status);

    // A save file that will not parse is the store's InvalidDataException, and it is the only store fault
    // caught: anything else is a bug, and the host answers it as one.
    public static IResult Unreadable(InvalidDataException fault) =>
        Of(StatusCodes.Status500InternalServerError, "The saves file could not be read", fault.Message);
}
