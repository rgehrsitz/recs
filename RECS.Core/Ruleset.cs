// RECS/RECS.Core/Ruleset.cs

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RECS.Core
{
    public class Ruleset
    {
        [JsonPropertyName("version")]
        public string Version { get; init; } = "1.0";
        
        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; init; } = new List<Rule>();

        public static Ruleset Parse(string jsonData)
        {
            try
            {
                var ruleset = JsonSerializer.Deserialize<Ruleset>(jsonData);
                if (ruleset == null || ruleset.Rules.Count == 0)
                {
                    throw new Exception("Ruleset is empty or invalid.");
                }

                foreach (var rule in ruleset.Rules)
                {
                    rule.Validate(); // Ensure the rule and its conditions are valid
                }

                return ruleset;
            }
            catch (JsonException ex)
            {
                throw new Exception("Failed to parse JSON data.", ex);
            }
        }
    }

    public class Rule
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("priority")]
        public int Priority { get; init; }

        [JsonPropertyName("conditions")]
        public ConditionGroup Conditions { get; init; } = new();

        [JsonPropertyName("actions")]
        public List<RuleAction> Actions { get; init; } = [];

        [JsonPropertyName("scripts")]
        public Dictionary<string, UserScript>? Scripts { get; init; } // Nullable, as not every rule has a script.
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
        
        public bool Evaluate(Dictionary<string, object> facts)
        {
            // Implement rule evaluation logic here
            return Conditions.Evaluate(facts);
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new Exception("Rule name is required.");
            }
            if (Priority < 0)
            {
                throw new Exception($"Rule '{Name}' has a negative priority.");
            }

            Conditions.Validate();
            foreach (var action in Actions)
            {
                action.Validate();
            }
        }
    }

    public class ConditionGroup
    {
        [JsonPropertyName("all")]
        public List<ConditionOrGroup>? All { get; init; } // Nullable, as a rule may not have both/all/any conditions.

        [JsonPropertyName("any")]
        public List<ConditionOrGroup>? Any { get; set; } // Nullable for the same reason as 'All'.
        
        public bool Evaluate(Dictionary<string, object> facts)
        {
            bool allResult = All?.All(c => c.Evaluate(facts)) ?? true;
            bool anyResult = Any?.Any(c => c.Evaluate(facts)) ?? true;
            return allResult && anyResult;
        }

        public void Validate()
        {
            if ((All == null || All.Count == 0) && (Any == null || Any.Count == 0))
            {
                throw new Exception("At least one condition (all or any) must be defined.");
            }

            foreach (var condition in All ?? new List<ConditionOrGroup>())
            {
                condition.Validate();
            }

            foreach (var condition in Any ?? new List<ConditionOrGroup>())
            {
                condition.Validate();
            }
        }
    }

    public class ConditionOrGroup
    {
        [JsonPropertyName("fact")]
        public string Fact { get; init; } = string.Empty;

        [JsonPropertyName("operator")]
        public string Operator { get; init; } = string.Empty;

        private object? _value; // Backing field for the value

        [JsonPropertyName("value")]
        public JsonElement JsonValue { get; set; } // Temporarily hold the JsonElement

        [JsonIgnore]
        public object Value
        {
            get
            {
                // Return the backing field if it has been set
                if (_value != null)
                {
                    return _value;
                }

                // Otherwise, return based on JsonValue conversion
                switch (JsonValue.ValueKind)
                {
                    case JsonValueKind.Number:
                        if (JsonValue.TryGetInt32(out int intValue))
                        {
                            return intValue;
                        }
                        if (JsonValue.TryGetDouble(out double doubleValue))
                        {
                            return doubleValue;
                        }
                        break;
                    case JsonValueKind.String:
                        return JsonValue.GetString() ?? string.Empty;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return JsonValue.GetBoolean();
                }

                throw new InvalidOperationException(
                    $"Unsupported fact value type: {JsonValue.ValueKind}"
                );
            }
            init => _value = value;
        }

        [JsonPropertyName("all")]
        public List<ConditionOrGroup>? All { get; set; }

        [JsonPropertyName("any")]
        public List<ConditionOrGroup>? Any { get; set; }
        
        public bool Evaluate(Dictionary<string, object> facts)
        {
            if (All != null || Any != null)
            {
                bool allResult = All?.All(c => c.Evaluate(facts)) ?? true;
                bool anyResult = Any?.Any(c => c.Evaluate(facts)) ?? true;
                return allResult && anyResult;
            }

            if (!facts.TryGetValue(Fact, out var factValue))
            {
                return false;
            }

            return CompareValues(factValue, Value, Operator);
        }

        private bool CompareValues(object factValue, object conditionValue, string @operator)
        {
            // Implement comparison logic based on the operator
            // This is a simplified version; you may need to handle more cases
            return @operator switch
            {
                "EQ" => factValue.Equals(conditionValue),
                "NEQ" => !factValue.Equals(conditionValue),
                "GT" => Convert.ToDouble(factValue) > Convert.ToDouble(conditionValue),
                "LT" => Convert.ToDouble(factValue) < Convert.ToDouble(conditionValue),
                "GTE" => Convert.ToDouble(factValue) >= Convert.ToDouble(conditionValue),
                "LTE" => Convert.ToDouble(factValue) <= Convert.ToDouble(conditionValue),
                "CONTAINS" => factValue.ToString()!.Contains(conditionValue.ToString() ?? string.Empty),
                "NOT_CONTAINS" => !factValue.ToString()!.Contains(conditionValue.ToString() ?? string.Empty),
                _ => throw new NotSupportedException($"Operator '{@operator}' is not supported."),
            };
        }

        public void Validate()
        {
            if (All == null && Any == null && string.IsNullOrWhiteSpace(Fact))
            {
                throw new Exception(
                    "Either a fact or nested conditions (all/any) must be provided."
                );
            }

            if (string.IsNullOrWhiteSpace(Operator))
            {
                throw new Exception($"Operator is missing for fact '{Fact}'.");
            }

            foreach (var condition in All ?? new List<ConditionOrGroup>())
            {
                condition.Validate();
            }

            foreach (var condition in Any ?? new List<ConditionOrGroup>())
            {
                condition.Validate();
            }
        }
    }

    public class RuleAction
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public object Value { get; init; } = new object();
        
        public void Execute(Dictionary<string, object> facts)
        {
            // Implement action execution logic here
            Console.WriteLine($"Executing action: {Type} on {Target} with value {Value}");
            // You may want to implement more sophisticated action handling based on the Type
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Type))
            {
                throw new Exception("Action type is required.");
            }
        }
    }

    public class UserScript
    {
        [JsonPropertyName("params")]
        public List<string>? Params { get; set; } // Nullable, because not all scripts may have parameters.

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
        
        public Func<Dictionary<string, object>, object> Compile()
        {
            // This is a placeholder. You'll need to implement actual script compilation logic
            // using a JavaScript engine like Jint or a C# scripting solution.
            return (facts) => {
                Console.WriteLine($"Executing script with params: {string.Join(", ", Params ?? new List<string>())}");
                return null!;
            };
        }
    }
}
