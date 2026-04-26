using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Tests.Domain;

public class PersonaTests
{
    [Fact]
    public void Persona_DefaultsAreSane()
    {
        var p = new Persona
        {
            LastName = "Иванов",
            FirstName = "Иван",
            Phone = "+7-999-000-00-00"
        };

        Assert.Equal("Иванов", p.LastName);
        Assert.Equal("Иван", p.FirstName);
        Assert.Null(p.Email);
        Assert.Null(p.Gender);
    }

    [Fact]
    public void Booking_DefaultStatusIsCreated()
    {
        var b = new Booking();
        Assert.Equal(BookingStatus.Created, b.Status);
    }
}
