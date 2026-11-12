namespace OPEN_LRD.NLP
{
    internal static class Lexicon
    {
        private static readonly HashSet<string> determiners = Create(
            "a", "an", "the", "this", "that", "these", "those", "my", "your", "his",
            "her", "its", "our", "their", "some", "any", "no", "every", "each",
            "either", "neither", "another", "both", "all", "few", "many", "much",
            "several");

        private static readonly HashSet<string> pronouns = Create(
            "i", "me", "you", "he", "him", "she", "her", "it", "we", "us", "they",
            "them", "myself", "yourself", "himself", "herself", "itself", "ourselves",
            "yourselves", "themselves", "someone", "something", "anyone", "anything",
            "everyone", "everything", "nobody", "nothing", "none");

        private static readonly Dictionary<string, PronounInfo> pronounInfos =
            new Dictionary<string, PronounInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["he"] = ThirdPronoun("he", PronounNumber.Singular, PronounCase.Subject),
                ["him"] = ThirdPronoun("him", PronounNumber.Singular, PronounCase.Object),
                ["his"] = ThirdPronoun("his", PronounNumber.Singular, PronounCase.Possessive),
                ["she"] = ThirdPronoun("she", PronounNumber.Singular, PronounCase.Subject),
                ["her"] = ThirdPronoun("her", PronounNumber.Singular, PronounCase.Object),
                ["hers"] = ThirdPronoun("hers", PronounNumber.Singular, PronounCase.Possessive),
                ["it"] = ThirdPronoun("it", PronounNumber.Singular, PronounCase.Subject),
                ["its"] = ThirdPronoun("its", PronounNumber.Singular, PronounCase.Possessive),
                ["they"] = ThirdPronoun("they", PronounNumber.Plural, PronounCase.Subject),
                ["them"] = ThirdPronoun("them", PronounNumber.Plural, PronounCase.Object),
                ["their"] = ThirdPronoun("their", PronounNumber.Plural, PronounCase.Possessive),
                ["theirs"] = ThirdPronoun("theirs", PronounNumber.Plural, PronounCase.Possessive),
                ["himself"] = ReflexivePronoun("himself", PronounPerson.Third, PronounNumber.Singular),
                ["herself"] = ReflexivePronoun("herself", PronounPerson.Third, PronounNumber.Singular),
                ["itself"] = ReflexivePronoun("itself", PronounPerson.Third, PronounNumber.Singular),
                ["themselves"] = ReflexivePronoun("themselves", PronounPerson.Third, PronounNumber.Plural),
                ["i"] = PersonalPronoun("i", PronounPerson.First, PronounNumber.Singular, PronounCase.Subject),
                ["me"] = PersonalPronoun("me", PronounPerson.First, PronounNumber.Singular, PronounCase.Object),
                ["my"] = PersonalPronoun("my", PronounPerson.First, PronounNumber.Singular, PronounCase.Possessive),
                ["mine"] = PersonalPronoun("mine", PronounPerson.First, PronounNumber.Singular, PronounCase.Possessive),
                ["myself"] = ReflexivePronoun("myself", PronounPerson.First, PronounNumber.Singular),
                ["we"] = PersonalPronoun("we", PronounPerson.First, PronounNumber.Plural, PronounCase.Subject),
                ["us"] = PersonalPronoun("us", PronounPerson.First, PronounNumber.Plural, PronounCase.Object),
                ["our"] = PersonalPronoun("our", PronounPerson.First, PronounNumber.Plural, PronounCase.Possessive),
                ["ours"] = PersonalPronoun("ours", PronounPerson.First, PronounNumber.Plural, PronounCase.Possessive),
                ["ourselves"] = ReflexivePronoun("ourselves", PronounPerson.First, PronounNumber.Plural),
                ["you"] = PersonalPronoun("you", PronounPerson.Second, PronounNumber.Unknown, PronounCase.Subject),
                ["your"] = PersonalPronoun("your", PronounPerson.Second, PronounNumber.Unknown, PronounCase.Possessive),
                ["yours"] = PersonalPronoun("yours", PronounPerson.Second, PronounNumber.Unknown, PronounCase.Possessive),
                ["yourself"] = ReflexivePronoun("yourself", PronounPerson.Second, PronounNumber.Singular),
                ["yourselves"] = ReflexivePronoun("yourselves", PronounPerson.Second, PronounNumber.Plural),
                ["someone"] = IndefinitePronoun("someone"),
                ["something"] = IndefinitePronoun("something"),
                ["anyone"] = IndefinitePronoun("anyone"),
                ["anything"] = IndefinitePronoun("anything"),
                ["everyone"] = IndefinitePronoun("everyone"),
                ["everything"] = IndefinitePronoun("everything"),
                ["nobody"] = IndefinitePronoun("nobody"),
                ["nothing"] = IndefinitePronoun("nothing"),
                ["none"] = IndefinitePronoun("none")
            };

        private static readonly HashSet<string> whWords = Create(
            "who", "whom", "whose", "what", "which", "when", "where", "why", "how");

        private static readonly HashSet<string> auxiliaries = Create(
            "am", "is", "are", "was", "were", "be", "been", "being", "have", "has",
            "had", "having", "do", "does", "did", "doing", "done", "can", "could",
            "will", "would", "shall", "should", "may", "might", "must", "ought");

        private static readonly HashSet<string> prepositions = Create(
            "about", "above", "across", "after", "against", "along", "among", "around",
            "at", "before", "behind", "below", "beneath", "beside", "between", "beyond",
            "by", "despite", "during", "except", "for", "from", "in", "inside", "into",
            "like", "near", "of", "off", "on", "onto", "outside", "over", "past",
            "since", "through", "throughout", "to", "toward", "towards", "under",
            "until", "up", "upon", "with", "within", "without");

        private static readonly HashSet<string> conjunctions = Create(
            "and", "or", "but", "nor", "so", "yet", "because", "although", "though",
            "while", "whereas", "if", "unless", "whether", "that", "than", "as");

        private static readonly HashSet<string> commonVerbs = Create(
            "accept", "add", "agree", "allow", "answer", "appear", "arrive", "ask", "believe",
            "bring", "build", "buy", "call", "carry", "cause", "change", "choose",
            "close", "come", "continue", "create", "cut", "decide", "describe", "die",
            "draw", "drink", "drive", "eat", "end", "expect", "explain", "fall", "feel",
            "fight", "find", "finish", "fix", "fly", "follow", "forget", "get", "give",
            "go", "grow", "happen", "hear", "help", "hold", "hope", "include", "keep",
            "know", "lead", "leave", "let", "like", "live", "look", "lose", "love",
            "make", "manage", "mean", "meet", "move", "need", "notice", "open", "pass",
            "pay", "play", "practice", "promise", "put", "raise", "read", "reduce",
            "remain", "remember", "require", "run", "say", "see", "seem", "sell",
            "send", "serve", "set", "share", "show", "sit", "sleep", "smile", "speak",
            "spend", "stand", "start", "stay", "stop", "study", "take", "talk", "taste",
            "teach", "tell", "think", "throw", "travel", "try", "turn", "understand",
            "use", "visit", "wait", "walk", "want", "watch", "win", "work", "write");

        private static readonly HashSet<string> commonAdjectives = Create(
            "able", "bad", "best", "better", "big", "black", "blue", "brown", "brief", "certain",
            "clear", "cold", "cool", "dark", "different", "difficult", "easy", "false",
            "fast", "few", "first", "fictional", "good", "great", "green", "happy",
            "hard", "high", "hot", "important", "kind", "large", "last", "light",
            "likely", "little", "long", "low", "many", "much", "new", "next", "nice",
            "old", "permanent", "poor", "possible", "quick", "real", "red", "rich",
            "sad", "safe", "same", "second", "short", "simple", "slow", "small", "smart",
            "strong", "tall", "temporary", "true", "warm", "weak", "well", "white",
            "young");

        private static readonly HashSet<string> commonAdverbs = Create(
            "almost", "already", "also", "always", "here", "just", "maybe", "never",
            "not", "now", "often", "only", "perhaps", "quickly", "really", "slowly",
            "sometimes", "still", "then", "there", "today", "tomorrow", "too", "very",
            "well", "yesterday", "yet");

        private static readonly HashSet<string> abbreviations = Create(
            "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st", "vs", "etc", "fig",
            "inc", "ltd", "co", "corp", "dept", "univ", "approx", "ave", "blvd", "rd",
            "mt", "ft");

        private static readonly HashSet<string> locationGazetteer = Create(
            "africa", "asia", "australia", "berlin", "california", "canada",
            "china", "england", "europe", "france", "germany", "india", "italy",
            "japan", "london", "mexico", "new york", "paris", "seattle", "spain",
            "tokyo", "united states", "usa", "washington", "york");

        private static readonly HashSet<string> organizationGazetteer = Create(
            "acme", "amazon", "apple", "google", "ibm", "microsoft", "nasa",
            "openai", "united nations");

        private static readonly HashSet<string> organizationCues = Create(
            "agency", "bank", "co", "college", "company", "corp", "corporation",
            "department", "foundation", "group", "hospital", "inc",
            "incorporated", "institute", "laboratories", "labs", "limited", "llc",
            "ltd", "ministry", "school", "university");

        private static readonly HashSet<string> personTitles = Create(
            "dr", "lady", "lord", "madam", "miss", "mr", "mrs", "ms", "prof",
            "sir");

        private static readonly HashSet<string> personFirstNames = Create(
            "alice", "bob", "carol", "david", "emma", "frank", "grace", "henry",
            "irene", "jane", "john", "karen", "liam", "mary", "michael", "nancy",
            "peter", "sarah", "thomas", "william");

        private static readonly HashSet<string> eventNouns = Create(
            "appointment", "arrival", "celebration", "ceremony", "class",
            "conference", "concert", "departure", "dinner", "election", "event",
            "festival", "game", "interview", "launch", "lecture", "match",
            "meeting", "party", "presentation", "review", "trip", "war",
            "wedding");

        private static readonly HashSet<string> numberWords = Create(
            "zero", "one", "two", "three", "four", "five", "six", "seven",
            "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen",
            "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty",
            "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
            "hundred", "thousand", "million", "billion", "dozen");

        private static readonly HashSet<string> timeWords = Create(
            "afternoon", "evening", "midnight", "morning", "night", "noon",
            "today", "tomorrow", "tonight", "yesterday");

        private static readonly Dictionary<string, string> irregularLemmas =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["am"] = "be", ["is"] = "be", ["are"] = "be", ["was"] = "be",
                ["were"] = "be", ["been"] = "be", ["being"] = "be",
                ["has"] = "have", ["had"] = "have", ["having"] = "have",
                ["does"] = "do", ["did"] = "do", ["done"] = "do", ["doing"] = "do",
                ["went"] = "go", ["gone"] = "go", ["said"] = "say", ["saw"] = "see",
                ["seen"] = "see", ["made"] = "make", ["took"] = "take", ["taken"] = "take",
                ["came"] = "come", ["got"] = "get", ["gotten"] = "get", ["gave"] = "give",
                ["given"] = "give", ["knew"] = "know", ["known"] = "know",
                ["thought"] = "think", ["told"] = "tell", ["became"] = "become",
                ["shown"] = "show", ["left"] = "leave", ["felt"] = "feel",
                ["brought"] = "bring", ["began"] = "begin", ["begun"] = "begin",
                ["kept"] = "keep", ["held"] = "hold", ["wrote"] = "write",
                ["written"] = "write", ["stood"] = "stand", ["heard"] = "hear",
                ["meant"] = "mean", ["met"] = "meet", ["ran"] = "run", ["paid"] = "pay",
                ["sat"] = "sit", ["spoke"] = "speak", ["spoken"] = "speak",
                ["led"] = "lead", ["grew"] = "grow", ["grown"] = "grow",
                ["lost"] = "lose", ["fell"] = "fall", ["fallen"] = "fall",
                ["sent"] = "send", ["built"] = "build", ["understood"] = "understand",
                ["drew"] = "draw", ["drawn"] = "draw", ["broke"] = "break",
                ["broken"] = "break", ["spent"] = "spend", ["rose"] = "rise",
                ["risen"] = "rise", ["drove"] = "drive", ["driven"] = "drive",
                ["bought"] = "buy", ["wore"] = "wear", ["worn"] = "wear",
                ["chose"] = "choose", ["chosen"] = "choose", ["ate"] = "eat",
                ["eaten"] = "eat", ["drank"] = "drink", ["drunk"] = "drink",
                ["swam"] = "swim", ["swum"] = "swim", ["flew"] = "fly",
                ["flown"] = "fly", ["sang"] = "sing", ["sung"] = "sing",
                ["slept"] = "sleep", ["taught"] = "teach", ["caught"] = "catch",
                ["fought"] = "fight", ["sought"] = "seek", ["sold"] = "sell",
                ["won"] = "win", ["forgot"] = "forget", ["forgotten"] = "forget",
                ["forgave"] = "forgive", ["forgiven"] = "forgive",
                ["threw"] = "throw", ["thrown"] = "throw", ["blew"] = "blow",
                ["blown"] = "blow", ["children"] = "child", ["people"] = "person",
                ["men"] = "man", ["women"] = "woman", ["mice"] = "mouse",
                ["geese"] = "goose", ["teeth"] = "tooth", ["feet"] = "foot",
                ["data"] = "datum"
            };

        private static readonly HashSet<string> irregularNounForms = Create(
            "children", "people", "men", "women", "mice", "geese", "teeth", "feet",
            "data");

        private static HashSet<string> Create(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        private static PronounInfo ThirdPronoun(
            string surface,
            PronounNumber number,
            PronounCase pronounCase)
        {
            return PersonalPronoun(surface, PronounPerson.Third, number, pronounCase);
        }

        private static PronounInfo PersonalPronoun(
            string surface,
            PronounPerson person,
            PronounNumber number,
            PronounCase pronounCase)
        {
            return new PronounInfo(
                surface,
                surface,
                person,
                number,
                pronounCase,
                false,
                false,
                person == PronounPerson.Third);
        }

        private static PronounInfo ReflexivePronoun(
            string surface,
            PronounPerson person,
            PronounNumber number)
        {
            return new PronounInfo(
                surface,
                surface,
                person,
                number,
                PronounCase.Object,
                true,
                false,
                false);
        }

        private static PronounInfo IndefinitePronoun(string surface)
        {
            return new PronounInfo(
                surface,
                surface,
                PronounPerson.Unknown,
                PronounNumber.Unknown,
                PronounCase.Unknown,
                false,
                true,
                false);
        }

        public static bool TryGetPronounInfo(string value, out PronounInfo info)
        {
            info = null!;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            return pronounInfos.TryGetValue(value.Trim(), out info!);
        }

        public static bool IsDeterminer(string value) => determiners.Contains(value);

        public static bool IsPronoun(string value) => pronouns.Contains(value);

        public static bool IsWhWord(string value) => whWords.Contains(value);

        public static bool IsAuxiliary(string value) => auxiliaries.Contains(value);

        public static bool IsPreposition(string value) => prepositions.Contains(value);

        public static bool IsConjunction(string value) => conjunctions.Contains(value);

        public static bool IsKnownVerb(string value) => commonVerbs.Contains(value);

        public static bool IsKnownAdjective(string value) => commonAdjectives.Contains(value);

        public static bool IsKnownAdverb(string value) => commonAdverbs.Contains(value);

        public static bool IsAbbreviation(string value) => abbreviations.Contains(value);

        public static bool IsLocationWord(string value) => locationGazetteer.Contains(value);

        public static bool IsOrganizationWord(string value)
        {
            return organizationGazetteer.Contains(value) || organizationCues.Contains(value);
        }

        public static bool IsPersonTitle(string value) => personTitles.Contains(value);

        public static bool IsPersonName(string value) => personFirstNames.Contains(value);

        public static bool IsEventNoun(string value) => eventNouns.Contains(value);

        public static bool IsNumberWord(string value) => numberWords.Contains(value);

        public static bool IsTimeWord(string value) => timeWords.Contains(value);

        public static bool TryGetIrregularLemma(string value, out string lemma)
        {
            return irregularLemmas.TryGetValue(value, out lemma!);
        }

        public static bool IsIrregularVerbForm(string value)
        {
            return irregularLemmas.ContainsKey(value) && !irregularNounForms.Contains(value);
        }

        public static bool IsNumberOrDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            bool hasDigit = false;

            foreach (char character in value)
            {
                if (char.IsDigit(character))
                {
                    hasDigit = true;
                    continue;
                }

                if (character is '.' or ',' or '-' or '/' or ':' or '%' or '+')
                    continue;

                return false;
            }

            return hasDigit;
        }

        public static bool LooksLikeDateWord(string value)
        {
            return value.ToLowerInvariant() switch
            {
                "january" or "february" or "march" or "april" or "may" or "june"
                    or "july" or "august" or "september" or "october" or "november"
                    or "december" or "monday" or "tuesday" or "wednesday" or "thursday"
                    or "friday" or "saturday" or "sunday" => true,
                _ => false
            };
        }
    }
}
