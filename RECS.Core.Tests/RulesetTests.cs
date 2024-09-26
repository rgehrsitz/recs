// RECS/RECS.Core.Tests/RulesetTests.cs

using System;
using Xunit;

namespace RECS.Core.Tests
{
    public class RulesetTests
    {
        [Fact]
        public void Parse_ValidJson_ReturnsRuleset()
        {
            string json = @"{
        'rules': [
            {
                'name': 'SampleRule',
                'priority': 1,
                'conditions': {
                    'all': [
                        {
                            'fact': 'temperature',
                            'operator': '>',
                            'value': 30
                        }
                    ],
                    'any': []
                },
                'actions': [
                    {
                        'type': 'print',
                        'target': 'console',
                        'value': 'Hello, World!'
                    }
                ]
            }
        ]
    }".Replace('\'', '"'); // Replace single quotes with double quotes to form valid JSON

            Ruleset ruleset = Ruleset.Parse(json);
            Assert.NotNull(ruleset);
            Assert.Single(ruleset.Rules);
            Assert.Equal("SampleRule", ruleset.Rules[0].Name);
        }

        [Fact]
        public void Parse_InvalidJson_ThrowsException()
        {
            string invalidJson = "{ 'rules': [] }".Replace('\'', '"');

            Assert.Throws<Exception>(() => Ruleset.Parse(invalidJson));
        }
    }
}
