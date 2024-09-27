// RECS/RECS.Core/Ruleset.cs

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RECS.Core
{
    public class Ruleset
    {
        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = new List<Rule>();

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
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("conditions")]
        public ConditionGroup Conditions { get; set; } = new ConditionGroup();

        [JsonPropertyName("actions")]
        public List<RuleAction> Actions { get; set; } = new List<RuleAction>();

        [JsonPropertyName("scripts")]
        public Dictionary<string, Script>? Scripts { get; set; } // Nullable, as not every rule has a script.

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

            Conditions?.Validate();
            foreach (var action in Actions)
            {
                action.Validate();
            }
        }
    }

    public class ConditionGroup
    {
        [JsonPropertyName("all")]
        public List<ConditionOrGroup>? All { get; set; } // Nullable, as a rule may not have both/all/any conditions.

        [JsonPropertyName("any")]
        public List<ConditionOrGroup>? Any { get; set; } // Nullable for the same reason as 'All'.

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
        public string Fact { get; set; } = string.Empty;

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object Value { get; set; } = new object();

        [JsonPropertyName("all")]
        public List<ConditionOrGroup>? All { get; set; } // Nullable, for nested conditions.

        [JsonPropertyName("any")]
        public List<ConditionOrGroup>? Any { get; set; } // Nullable, for nested conditions.

        public void Validate()
        {
            if (All == null && Any == null && string.IsNullOrWhiteSpace(Fact))
            {
                throw new Exception(
                    "Either a fact or nested conditions (all/any) must be provided."
                );
            }

            // Ensure the operator is provided if it's a single condition
            if (string.IsNullOrWhiteSpace(Operator))
            {
                throw new Exception($"Operator is missing for fact '{Fact}'.");
            }

            // Recursively validate nested conditions
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
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object Value { get; set; } = new object();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Type))
            {
                throw new Exception("Action type is required.");
            }
        }
    }

    public class Script
    {
        [JsonPropertyName("params")]
        public List<string>? Params { get; set; } // Nullable, because not all scripts may have parameters.

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }
}
