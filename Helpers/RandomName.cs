namespace SafetyOpsTests.Helpers;

public class AdversarialName
{
    public string Value { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class NameResult
{
    public string? PlainValue { get; set; }
    public AdversarialName? AdversarialValue { get; set; }
    public bool IsAdversarial => AdversarialValue != null;

    public string Resolve() => IsAdversarial ? AdversarialValue!.Value : PlainValue!;
    public string? GetReason() => IsAdversarial ? AdversarialValue!.Reason : null;
}

public static class RandomName
{
    private static readonly Random _random = new();

    private static readonly string[] FirstNames =
    [
        "Alex", "Jordan", "Morgan", "Taylor", "Casey", "Riley", "Drew", "Quinn",
        "Jamie", "Avery", "Blake", "Cameron", "Dakota", "Emery", "Finley", "Hayden",
        "Jesse", "Kennedy", "Logan", "Mackenzie", "Noah", "Peyton", "Reese", "Sage",
        "Renée", "Amélie", "Björn", "Søren", "Łukasz", "Siobhán", "Niamh",
        "Nguyễn", "Trần", "Phạm", "Jean-Pierre", "Mary-Kate", "Anne-Marie",
        "Yi", "Bo", "Li", "Xi", "Su", "Wu", "Lu", "Yu",
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Rivera", "Nguyen", "Patel", "Kim", "Okafor", "Brennan", "Walsh",
        "Anderson", "Baker", "Campbell", "Davis", "Evans", "Foster", "Garcia",
        "Müller", "Schäfer", "Weiß", "García", "González", "López",
        "O'Brien", "O'Connor", "O'Sullivan", "McDonald", "MacGregor",
        "de la Cruz", "van der Berg", "von Braun", "St. Claire",
        "Čapek", "Dvořák", "Novák", "Ångström", "Ørsted",
    ];

    private static readonly string[] MiddleNames =
    [
        "Lee", "Ray", "Jo", "Mae", "James", "Lynn", "Kai", "Avery",
        "Ann", "Beth", "Brooke", "Claire", "Dawn", "Dean", "Elaine",
        "René", "Céleste", "José", "María", "D'Angelo", "D'Arcy",
        "A", "E", "I", "J", "K", "L", "M", "R",
    ];

    private static readonly AdversarialName[] AdversarialFirstNames =
    [
        new() { Value = "O'Shea", Reason = "apostrophe - SQL injection risk" },
        new() { Value = "John'; DROP TABLE users;--", Reason = "classic SQL injection" },
        new() { Value = "<script>alert(\"xss\")</script>", Reason = "XSS script tag injection" },
        new() { Value = "Jean-Marie", Reason = "hyphenated first name" },
        new() { Value = "A", Reason = "single character name" },
        new() { Value = "Renée", Reason = "French diacritic" },
        new() { Value = "Søren", Reason = "Danish ø character" },
        new() { Value = "Björn", Reason = "Swedish umlaut ö" },
        new() { Value = "Siobhán", Reason = "Irish - silent letters + fada accent" },
        new() { Value = "Łukasz", Reason = "Polish Ł character" },
        new() { Value = "Nguyễn", Reason = "Vietnamese tonal diacritics" },
        new() { Value = "محمد", Reason = "Arabic script - RTL text" },
        new() { Value = "山田", Reason = "CJK characters (Japanese)" },
        new() { Value = "NULL", Reason = "literal string NULL - ORM edge case" },
        new() { Value = "null", Reason = "lowercase null - JSON edge case" },
        new() { Value = "", Reason = "empty string - validation boundary" },
        new() { Value = "   ", Reason = "whitespace-only string" },
        new() { Value = "A".PadRight(255, 'A'), Reason = "max VARCHAR(255) length" },
        new() { Value = "${7*7}", Reason = "template literal / SSTI probe" },
        new() { Value = "{{7*7}}", Reason = "Jinja/Handlebars SSTI probe" },
        new() { Value = "../../../etc/passwd", Reason = "path traversal probe" },
    ];

    private static readonly AdversarialName[] AdversarialLastNames =
    [
        new() { Value = "O'Brien", Reason = "Irish apostrophe - SQL injection risk" },
        new() { Value = "' OR '1'='1", Reason = "SQL injection in last name field" },
        new() { Value = "de la Cruz", Reason = "multi-word Spanish surname" },
        new() { Value = "van der Berg", Reason = "Dutch multi-word surname" },
        new() { Value = "García-López", Reason = "hyphenated compound Spanish surname" },
        new() { Value = "Müller", Reason = "German umlaut ü" },
        new() { Value = "Weiß", Reason = "German sharp s (ß)" },
        new() { Value = "NULL", Reason = "literal string NULL" },
        new() { Value = "MC ALLISTER", Reason = "all-caps surname" },
        new() { Value = "Brown & Sons", Reason = "ampersand - HTML entity risk" },
        new() { Value = "<Johnson>", Reason = "angle brackets - XSS risk" },
    ];

    private static readonly AdversarialName[] AdversarialMiddleNames =
    [
        new() { Value = "A", Reason = "single initial" },
        new() { Value = "J.", Reason = "initial with period" },
        new() { Value = "D'Angelo", Reason = "apostrophe prefix" },
        new() { Value = "Lee-Ann", Reason = "hyphenated middle name" },
        new() { Value = "'; SELECT 1;--", Reason = "SQL injection in middle name field" },
        new() { Value = "NULL", Reason = "literal NULL string" },
        new() { Value = "", Reason = "empty middle name" },
        new() { Value = "María", Reason = "Spanish María with accent" },
    ];

    private static T PickRandom<T>(T[] list) => list[_random.Next(list.Length)];

    public static NameResult WeightedRandomFirstName(double adversarialWeight = 0.2) =>
        _random.NextDouble() < adversarialWeight
            ? new NameResult { AdversarialValue = PickRandom(AdversarialFirstNames) }
            : new NameResult { PlainValue = PickRandom(FirstNames) };

    public static NameResult WeightedRandomLastName(double adversarialWeight = 0.2) =>
        _random.NextDouble() < adversarialWeight
            ? new NameResult { AdversarialValue = PickRandom(AdversarialLastNames) }
            : new NameResult { PlainValue = PickRandom(LastNames) };

    public static NameResult WeightedRandomMiddleName(double adversarialWeight = 0.2) =>
        _random.NextDouble() < adversarialWeight
            ? new NameResult { AdversarialValue = PickRandom(AdversarialMiddleNames) }
            : new NameResult { PlainValue = PickRandom(MiddleNames) };
}