﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

#pragma warning disable S103   // Lines should not be too long

namespace Microsoft.Extensions.AI;

public class OllamaEmbeddingGeneratorTests
{
    [Fact]
    public void Ctor_InvalidArgs_Throws()
    {
        Assert.Throws<ArgumentNullException>("endpoint", () => new OllamaEmbeddingGenerator((string)null!));
        Assert.Throws<ArgumentException>("modelId", () => new OllamaEmbeddingGenerator(new Uri("http://localhost"), "   "));
    }

    [Fact]
    public void GetService_SuccessfullyReturnsUnderlyingClient()
    {
        using OllamaEmbeddingGenerator generator = new("http://localhost");

        Assert.Same(generator, generator.GetService<OllamaEmbeddingGenerator>());
        Assert.Same(generator, generator.GetService<IEmbeddingGenerator<string, Embedding<float>>>());

        using IEmbeddingGenerator<string, Embedding<float>> pipeline = generator
            .AsBuilder()
            .UseOpenTelemetry()
            .UseDistributedCache(new MemoryDistributedCache(Options.Options.Create(new MemoryDistributedCacheOptions())))
            .Build();

        Assert.NotNull(pipeline.GetService<DistributedCachingEmbeddingGenerator<string, Embedding<float>>>());
        Assert.NotNull(pipeline.GetService<CachingEmbeddingGenerator<string, Embedding<float>>>());
        Assert.NotNull(pipeline.GetService<OpenTelemetryEmbeddingGenerator<string, Embedding<float>>>());

        Assert.Same(generator, pipeline.GetService<OllamaEmbeddingGenerator>());
        Assert.IsType<OpenTelemetryEmbeddingGenerator<string, Embedding<float>>>(pipeline.GetService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Fact]
    public void AsIEmbeddingGenerator_ProducesExpectedMetadata()
    {
        Uri endpoint = new("http://localhost/some/endpoint");
        string model = "amazingModel";

        using IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaEmbeddingGenerator(endpoint, model);
        var metadata = generator.GetService<EmbeddingGeneratorMetadata>();
        Assert.Equal("ollama", metadata?.ProviderName);
        Assert.Equal(endpoint, metadata?.ProviderUri);
        Assert.Equal(model, metadata?.DefaultModelId);
    }

    [Fact]
    public async Task GetEmbeddingsAsync_ExpectedRequestResponse()
    {
        const string Input = """
            {"model":"all-minilm","input":["hello, world!","red, white, blue"]}
            """;

        const string Output = """
            {
                "model":"all-minilm",
                "embeddings":[
                    [-0.038159743,0.032830726,-0.005602915,0.014363416,-0.04031945,-0.11662117,0.031710647,0.0019634133,-0.042558126,0.02925818,0.04254404,0.032178584,0.029820565,0.010947956,-0.05383333,-0.05031401,-0.023460664,0.010746779,-0.13776828,0.003972192,0.029283607,0.06673441,-0.015434976,0.048401773,-0.088160664,-0.012700827,0.04134059,0.0408592,-0.050058633,-0.058048956,0.048720006,0.068883754,0.0588242,0.008813041,-0.016036017,0.08514798,-0.07813561,-0.07740018,0.020856613,0.016228318,0.032506905,-0.053466275,-0.06220645,-0.024293836,0.0073994277,0.02410873,0.006477103,0.051144805,0.072868116,0.03460658,-0.0547553,-0.05937917,-0.007205277,0.020145971,0.035794333,0.005588114,0.010732389,-0.052755248,0.01006711,-0.008716047,-0.062840104,0.038445882,-0.013913384,0.07341423,0.09004691,-0.07995187,-0.016410379,0.044806693,-0.06886798,-0.03302609,-0.015488586,0.0112944925,0.03645402,0.06637969,-0.054364193,0.008732196,0.012049053,-0.038111813,0.006928739,0.05113517,0.07739711,-0.12295967,0.016389083,0.049567502,0.03162499,-0.039604694,0.0016613991,0.009564599,-0.03268798,-0.033994347,-0.13328508,0.0072719813,-0.010261588,0.038570367,-0.093384996,-0.041716397,0.069951184,-0.02632818,-0.149702,0.13445856,0.037486482,0.052814852,0.045044158,0.018727085,0.05445453,0.01727433,-0.032474063,0.046129994,-0.046679277,-0.03058037,-0.0181755,-0.048695795,0.033057086,-0.0038555008,0.050006237,-0.05828653,-0.010029618,0.01062073,-0.040105496,-0.0015263702,0.060846698,-0.04557025,0.049251337,0.026121102,0.019804202,-0.0016694543,0.059516467,-6.525171e-33,0.06351319,0.0030810465,0.028928237,0.17336167,0.0029677018,0.027755935,-0.09513812,-0.031182382,0.026697554,-0.0107956175,0.023849761,0.02378595,-0.03121345,0.049473017,-0.02506533,0.101713106,-0.079133175,-0.0032418896,0.04290832,0.094838716,-0.06652884,0.0062877694,0.02221229,0.0700068,-0.007469806,-0.0017550732,0.027011596,-0.075321496,0.114022695,0.0085597,-0.023766534,-0.04693697,0.014437173,0.01987886,-0.0046902793,0.0013660098,-0.034307938,-0.054156985,-0.09417741,-0.028919358,-0.018871028,0.04574328,0.047602862,-0.0031305805,-0.033291575,-0.0135114025,0.051019657,0.031115327,0.015239397,0.05413997,-0.085031144,0.013366392,-0.04757861,0.07102588,-0.013105953,-0.0023799809,0.050322797,-0.041649505,-0.014187793,0.0324716,0.005401626,0.091307014,0.0044665188,-0.018263677,-0.015284639,-0.04634121,0.038754962,0.014709013,0.052040145,0.0017918312,-0.014979437,0.027103048,0.03117813,0.023749126,-0.004567645,0.03617759,0.06680814,-0.001835277,0.021281,-0.057563916,0.019137124,0.031450257,-0.018432263,-0.040860977,0.10391725,0.011970765,-0.014854915,-0.10521159,-0.012288272,-0.00041675335,-0.09510029,0.058300544,0.042590536,-0.025064372,-0.09454636,4.0064686e-33,0.13224861,0.0053342036,-0.033114634,-0.09096768,-0.031561732,-0.03395822,-0.07202013,0.12591493,-0.08332582,0.052816514,0.001065021,0.022002738,0.1040207,0.013038866,0.04092958,0.018689224,0.1142518,0.024801003,0.014596161,0.006195551,-0.011214642,-0.035760444,-0.037979998,0.011274433,-0.051305123,0.007884909,0.06734877,0.0033462204,-0.09284879,0.037033774,-0.022331867,0.039951596,-0.030730229,-0.011403805,-0.014458028,0.024968812,-0.097553216,-0.03536226,-0.037567392,-0.010149212,-0.06387594,0.025570663,0.02060328,0.037549157,-0.104355134,-0.02837097,-0.052078977,0.0128349,-0.05123587,-0.029060647,-0.09632806,-0.042301137,0.067175224,-0.030890828,-0.010358077,0.027408795,-0.028092034,0.010337195,0.04303845,0.022324203,0.00797792,0.056084383,0.040727936,0.092925824,0.01653155,-0.053750493,0.00046004262,0.050728552,0.04253214,-0.029197674,0.00926312,-0.010662153,-0.037244495,0.002277273,-0.030296732,0.07459592,0.002572513,-0.017561244,0.0028881067,0.03841156,0.007247727,0.045637112,0.039992437,0.014227117,-0.014297474,0.05854321,0.03632371,0.05527864,-0.02007574,-0.08043163,-0.030238612,-0.014929122,0.022335418,0.011954643,-0.06906099,-1.8807288e-8,-0.07850291,0.046684187,-0.023935271,0.063510746,0.024001691,0.0014455577,-0.09078209,-0.066868275,-0.0801402,0.005480386,0.053663295,0.10483363,-0.066864185,0.015531167,0.06711155,0.07081655,-0.031996343,0.020819444,-0.021926524,-0.0073062326,-0.010652819,0.0041180425,0.033138428,-0.0789938,0.03876969,-0.075220205,-0.015715994,0.0059789424,0.005140016,-0.06150612,0.041992374,0.09544083,-0.043187104,0.014401576,-0.10615426,-0.027936764,0.011047429,0.069572434,0.06690283,-0.074798405,-0.07852024,0.04276141,-0.034642085,-0.106051244,-0.03581038,0.051521253,0.06865896,-0.04999753,0.0154549,-0.06452052,-0.07598782,0.02603005,0.074413665,-0.012398757,0.13330704,0.07475513,0.051348723,0.02098748,-0.02679416,0.08896129,0.039944872,-0.041040305,0.031930625,0.018114654],
                    [0.007228383,-0.021804843,-0.07494023,-0.021707121,-0.021184582,0.09326986,0.10764054,-0.01918113,0.007439991,0.01367952,-0.034187328,-0.044076536,0.016042138,0.007507193,-0.016432272,0.025345335,0.010598066,-0.03832474,-0.14418823,-0.033625234,0.013156937,-0.0048872638,-0.08534306,-0.00003228713,-0.08900276,-0.00008128615,0.010332802,0.053303026,-0.050233904,-0.0879366,-0.064243905,-0.017168961,0.1284308,-0.015268303,-0.049664143,-0.07491954,0.021887481,0.015997978,-0.07967111,0.08744341,-0.039261423,-0.09904984,0.02936398,0.042995434,0.057036504,0.09063012,0.0000012311281,0.06120768,-0.050825767,-0.014443322,0.02879051,-0.002343813,-0.10176559,0.104563184,0.031316753,0.08251861,-0.041213628,-0.0217945,0.0649965,-0.011131547,0.018417398,-0.014460508,-0.05108664,0.11330918,0.01863208,0.006442521,-0.039408617,-0.03609412,-0.009156692,-0.0031261789,-0.010928502,-0.021108521,0.037411734,0.012443921,0.018142054,-0.0362644,0.058286663,-0.02733258,-0.052172586,-0.08320095,-0.07089281,-0.0970049,-0.048587535,0.055343032,0.048351917,0.06892102,-0.039993215,0.06344781,-0.084417015,0.003692423,-0.059397053,0.08186814,0.0029228176,-0.010551637,-0.058019258,0.092128515,0.06862907,-0.06558893,0.021121018,0.079212844,0.09616225,0.0045106052,0.039712362,-0.053576704,0.035097837,-0.04251009,-0.013761404,0.011582285,0.02387105,0.009042205,0.054141942,-0.051263757,-0.07984356,-0.020198742,-0.051623948,-0.0013434993,-0.05825417,-0.0026240738,0.0050159167,-0.06320204,0.07872169,-0.04051374,0.04671058,-0.05804034,-0.07103668,-0.07507343,0.015222599,-3.0948323e-33,0.0076309564,-0.06283016,0.024291662,0.12532257,0.013917241,0.04869009,-0.037988827,-0.035241846,-0.041410565,-0.033772282,0.018835608,0.081035286,-0.049912665,0.044602085,0.030495265,-0.009206943,0.027668765,0.011651487,-0.10254086,0.054472663,-0.06514106,0.12192646,0.048823033,-0.015688669,0.010323047,-0.02821445,-0.030832449,-0.035029083,-0.010604268,0.0014445938,0.08670387,0.01997448,0.0101131955,0.036524937,-0.033489946,-0.026745271,-0.04709222,0.015197909,0.018787097,-0.009976326,-0.0016434817,-0.024719588,-0.09179337,0.09343157,0.029579962,-0.015174558,0.071250066,0.010549244,0.010716396,0.05435638,-0.06391847,-0.031383075,0.007916095,0.012391228,-0.012053197,-0.017409964,0.013742709,0.0594159,-0.033767693,0.04505938,-0.0017214329,0.12797962,0.03223919,-0.054756388,0.025249248,-0.02273578,-0.04701282,-0.018718086,0.009820931,-0.06267794,-0.012644738,0.0068301614,0.093209736,-0.027372226,-0.09436381,0.003861504,0.054960024,-0.058553983,-0.042971537,-0.008994571,-0.08225824,-0.013560626,-0.01880568,0.0995795,-0.040887516,-0.0036491079,-0.010253542,-0.031025425,-0.006957114,-0.038943008,-0.090270124,-0.031345647,0.029613726,-0.099465184,-0.07469079,7.844707e-34,0.024241973,0.03597121,-0.049776066,0.05084303,0.006059542,-0.020719761,0.019962702,0.092246406,0.069408394,0.062306542,0.013837189,0.054749023,0.05090263,0.04100415,-0.02573441,0.09535842,0.036858294,0.059478357,0.0070162765,0.038462427,-0.053635903,0.05912332,-0.037887845,-0.0012995935,-0.068758026,0.0671618,0.029407106,-0.061569903,-0.07481879,-0.01849014,0.014240046,-0.08064838,0.028351007,0.08456427,0.016858438,0.02053254,0.06171099,-0.028964644,-0.047633287,0.08802184,0.0017116248,0.019451816,0.03419083,0.07152118,-0.027244413,-0.04888475,-0.10314279,0.07628554,-0.045991484,-0.023299307,-0.021448445,0.04111079,-0.036342163,-0.010670482,0.01950527,-0.0648448,-0.033299454,0.05782628,0.030278979,0.079154804,-0.03679649,0.031728156,-0.034912236,0.08817754,0.059208114,-0.02319613,-0.027045371,-0.018559752,-0.051946763,-0.010635224,0.048839167,-0.043925915,-0.028300019,-0.0039419765,0.044211324,-0.067469835,-0.027534118,0.005051618,-0.034172326,0.080007285,-0.01931061,-0.005759926,0.08765162,0.08372951,-0.093784876,0.011837292,0.019019455,0.047941882,0.05504541,-0.12475821,0.012822803,0.12833545,0.08005919,0.019278418,-0.025834465,-1.9763878e-8,0.05211108,0.024891146,-0.0015623684,0.0040500895,0.015101377,-0.0031462535,0.014759316,-0.041329216,-0.029255627,0.048599463,0.062482737,0.018376771,-0.066601776,0.014752581,0.07968402,-0.015090815,-0.12100162,-0.0014005995,0.0134423375,-0.0065814927,-0.01188529,-0.01107086,-0.059613306,0.030120188,0.0418596,-0.009260598,0.028435009,0.024893047,0.031339604,0.09501834,0.027570697,0.0636991,-0.056108754,-0.0329521,-0.114633024,-0.00981398,-0.060992315,0.027551433,0.0069592255,-0.059862003,0.0008075791,0.001507554,-0.028574942,-0.011227367,0.0056030746,-0.041190825,-0.09364463,-0.04459479,-0.055058934,-0.029972456,-0.028642913,-0.015199684,0.007875299,-0.034083385,0.02143902,-0.017395096,0.027429376,0.013198211,0.005065835,0.037760753,0.08974973,0.07598824,0.0050444477,0.014734193]
                ],
                "total_duration":375551700,
                "load_duration":354411900,
                "prompt_eval_count":9
            }
            """;

        using VerbatimHttpHandler handler = new(Input, Output);
        using HttpClient httpClient = new(handler);
        using IEmbeddingGenerator<string, Embedding<float>> generator = new OllamaEmbeddingGenerator("http://localhost:11434", "all-minilm", httpClient);

        var response = await generator.GenerateAsync([
            "hello, world!",
            "red, white, blue",
        ]);
        Assert.NotNull(response);
        Assert.Equal(2, response.Count);

        Assert.NotNull(response.Usage);
        Assert.Equal(9, response.Usage.InputTokenCount);
        Assert.Equal(9, response.Usage.TotalTokenCount);

        foreach (Embedding<float> e in response)
        {
            Assert.Equal("all-minilm", e.ModelId);
            Assert.NotNull(e.CreatedAt);
            Assert.Equal(384, e.Vector.Length);
            Assert.Contains(e.Vector.ToArray(), f => !f.Equals(0));
        }
    }
}
