namespace Aplicativo.Core.Validation;
public static class TripValidation
{
    public static int Distance(int initial, int final)
    {
        if (initial < 0 || final <= initial) throw new ArgumentException("O hodômetro final deve ser maior que o inicial.");
        return checked(final - initial);
    }
}
