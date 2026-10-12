using Garage.Domain.Attachments;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Reminders;
using Garage.Domain.Vehicles;

namespace Garage.Application.Demo;

/// <summary>
/// Os dados de exemplo: um Civic EK com histórico completo (inclusive uma modificação), lembretes em todos os estados e um
/// link público, e um Gol com o mínimo. Tudo é relativo a "hoje" (<c>IAppClock.Today</c>), para as datas nunca ficarem
/// no futuro nem os prazos dos lembretes mudarem de estado com o tempo, e é montado com os tipos do domínio, que impõem
/// as próprias regras na criação. Os registros levam fotos reais do Wikimedia Commons (e, no Coilover, uma nota de
/// exemplo em PDF); o autor e a licença de cada foto vão na descrição do registro, que a página pública mostra.
/// </summary>
internal static class DemoDataCatalog
{
    public static IReadOnlyList<DemoVehicle> Build(DateOnly today) => [Civic(today), Gol(today)];

    // Do mais antigo ao mais novo, com a km crescendo ~12.000 km/ano até a km atual (152.300).
    private static DemoVehicle Civic(DateOnly today) => new(
        Data: new VehicleData(
            VehicleType.Car, "Honda", "Civic", "Sedan LX 1.6 16V Mec. 4p", "EK",
            VehicleYear.From(1997, 1998, today.Year), Fuel.Gasoline, "Civic EK", Plate.From("ABC1D23"),
            Vin: null, Notes: null, Catalog: null),
        CurrentOdometer: new Odometer(152_300),
        Records:
        [
            NewRecord(today, RecordType.Maintenance, daysAgo: 700, km: 128_900, "Troca da correia dentada", Category.Engine,
                description: "Kit completo com tensor; bomba d'água trocada junto.",
                performedBy: "Auto Center Bom Motor", laborCost: 450.00m,
                parts: [NewPart("Kit correia dentada com tensor", 1, 389.90m, brand: "Gates"), NewPart("Bomba d'água", 1, 165.00m, brand: "Nakata")],
                attachments: [Photo("civic-correia-dentada.jpg", "correia-dentada.jpg", "Terabass (CC BY-SA 3.0)")]),
            NewRecord(today, RecordType.Maintenance, daysAgo: 560, km: 133_600, "Pastilhas de freio dianteiras", Category.Brakes,
                description: "Trocadas por desgaste; discos medidos e dentro do limite.",
                performedBy: "Auto Center Bom Motor", laborCost: 120.00m,
                parts: [NewPart("Pastilha de freio dianteira", 1, 148.00m, brand: "Cobreq")],
                attachments: [Photo("civic-pastilhas-freio.jpg", "pastilhas-freio.jpg", "Riley from Christchurch, New Zealand (CC BY 2.0)")]),
            NewRecord(today, RecordType.Maintenance, daysAgo: 430, km: 137_900, "Alinhamento e balanceamento", Category.WheelsTires,
                performedBy: "Pneus & Cia", laborCost: 160.00m,
                attachments: [Photo("civic-alinhamento.jpg", "alinhamento.jpg", "Mike Peel (www.mikepeel.net) (CC BY-SA 4.0)")]),
            NewRecord(today, RecordType.Modification, daysAgo: 300, km: 142_200, "Coilover", Category.Suspension,
                description: "Kit rosqueável regulável; altura ajustada para o uso diário.",
                performedBy: "Garage Performance", laborCost: 600.00m,
                replacedOriginalPart: "Amortecedores originais guardados",
                parts: [NewPart("Kit coilover rosqueável", 1, 3_890.00m, brand: "BC Racing")],
                attachments:
                [
                    Photo("civic-coilover.jpg", "coilover.jpg", "Fortune Auto North America (CC BY-SA 4.0)"),
                    Photo("civic-amortecedores-originais.jpg", "amortecedores-originais.jpg", "Shixart1985 (CC BY 2.0)"),
                    SampleInvoice("civic-coilover-nota-exemplo.pdf", "coilover-nota-exemplo.pdf")
                ]),
            NewRecord(today, RecordType.Maintenance, daysAgo: 130, km: 147_800, "Troca de óleo e filtro", Category.Engine,
                description: "Óleo 5W30 sintético.",
                performedBy: "Auto Center Bom Motor", laborCost: 60.00m,
                parts:
                [
                    NewPart("Óleo de motor 5W30 sintético", 3.5m, 52.00m, PartUnit.Liter, brand: "Mobil"),
                    NewPart("Filtro de óleo", 1, 28.90m, brand: "Tecfil")
                ],
                attachments:
                [
                    Photo("civic-oleo.jpg", "oleo.jpg", "Santeri Viinamäki (CC BY-SA 4.0)"),
                    Photo("civic-filtro-oleo.jpg", "filtro-oleo.jpg", "Harrikkamies (domínio público)")
                ]),
            NewRecord(today, RecordType.Maintenance, daysAgo: 10, km: 151_800, "Repintura do para-choque dianteiro", Category.BodyPaint,
                description: "Retoque de arranhões e repintura na cor original.",
                performedBy: "Funilaria Bom Acabamento", laborCost: 850.00m,
                attachments: [Photo("civic-lx-frente.jpg", "para-choque.jpg", "Elise240SX (CC BY-SA 4.0)")])
        ],
        Reminders:
        [
            // Faltam 500 km: "vence em breve" (o prazo de 6 meses ainda está longe).
            new DemoReminder("Troca de óleo e filtro", new ReminderInterval(5_000, 6), today.AddDays(-130), new Odometer(147_800)),
            // Em dia nos dois eixos: ~26.600 km e mais de 2 anos pela frente.
            new DemoReminder("Correia dentada", new ReminderInterval(50_000, 48), today.AddDays(-700), new Odometer(128_900)),
            // Só por prazo: venceu há cerca de 30 dias.
            new DemoReminder("Fluido de freio", new ReminderInterval(null, 24), today.AddDays(-760), null)
        ],
        PublicLink: new DemoShareLink("Link de exemplo", IncludeCosts: false, IncludePlate: false));

    private static DemoVehicle Gol(DateOnly today) => new(
        Data: new VehicleData(
            VehicleType.Car, "Volkswagen", "Gol", "1.0 MPI Flex 12V 5p", Generation: null,
            VehicleYear.From(2018, 2019, today.Year), Fuel.Flex, Nickname: null, Plate: null,
            Vin: null, Notes: null, Catalog: null),
        CurrentOdometer: new Odometer(48_000),
        Records:
        [
            NewRecord(today, RecordType.Maintenance, daysAgo: 200, km: 42_000, "Troca de óleo e filtro", Category.Engine,
                performedBy: "Oficina Mecânica Silva", laborCost: 50.00m,
                parts:
                [
                    NewPart("Óleo de motor 5W40 semissintético", 3.2m, 41.00m, PartUnit.Liter),
                    NewPart("Filtro de óleo", 1, 24.90m)
                ]),
            NewRecord(today, RecordType.Maintenance, daysAgo: 90, km: 45_200, "Troca dos quatro pneus", Category.WheelsTires,
                description: "Montagem e balanceamento incluídos na mão de obra.",
                performedBy: "Pneus & Cia", laborCost: 100.00m,
                parts: [NewPart("Pneu 175/70 R14", 4, 289.90m)],
                // A única foto fora do link público (o Gol nem tem link): exemplo da visibilidade por foto.
                attachments: [Photo("gol-pneus.jpg", "pneus.jpg", "ProjectManhattan (CC BY-SA 3.0)", isPublic: false)])
        ],
        Reminders:
        [
            // Faltam 4.000 km e ~5 meses: em dia, para o painel de pendências mostrar só o Civic.
            new DemoReminder("Troca de óleo e filtro", new ReminderInterval(10_000, 12), today.AddDays(-200), new Odometer(42_000))
        ],
        PublicLink: null);

    private static DemoRecord NewRecord(
        DateOnly today,
        RecordType type,
        int daysAgo,
        int km,
        string title,
        Category category,
        string? description = null,
        string? performedBy = null,
        decimal? laborCost = null,
        string? replacedOriginalPart = null,
        IReadOnlyList<PartData>? parts = null,
        IReadOnlyList<DemoAttachment>? attachments = null)
    {
        attachments ??= [];
        var details = new RecordDetails(
            type,
            today.AddDays(-daysAgo),
            new Odometer(km),
            title,
            WithPhotoCredits(description, attachments),
            category,
            performedBy,
            laborCost is { } labor ? new Money(labor) : null,
            replacedOriginalPart,
            parts ?? []);

        return new DemoRecord(details, attachments);
    }

    /// <summary>
    /// As fotos são de terceiros, em licenças que pedem o crédito: autor e licença entram numa linha no fim da descrição
    /// (a tela do registro e a página pública preservam a quebra de linha). A origem completa de cada arquivo fica em
    /// <c>Garage.Infrastructure/Demo/Assets/CREDITS.md</c>.
    /// </summary>
    private static string? WithPhotoCredits(string? description, IReadOnlyList<DemoAttachment> attachments)
    {
        var credits = attachments.Where(file => file.Credit is not null).Select(file => file.Credit!).ToList();
        if (credits.Count == 0)
            return description;

        var line = $"{(credits.Count == 1 ? "Crédito da foto" : "Crédito das fotos")} (Wikimedia Commons): {string.Join("; ", credits)}.";
        return description is null ? line : $"{description}\n{line}";
    }

    private static PartData NewPart(
        string name, decimal quantity, decimal unitPrice, PartUnit unit = PartUnit.Unit, string? brand = null) =>
        new(name, brand, PartNumber: null, quantity, unit, new Money(unitPrice));

    private static DemoAttachment Photo(string asset, string fileName, string credit, bool isPublic = true) =>
        new(asset, AttachmentKind.Photo, fileName, isPublic, credit);

    // Documento nunca é público (A4); o PDF é fictício e não precisa de crédito.
    private static DemoAttachment SampleInvoice(string asset, string fileName) =>
        new(asset, AttachmentKind.Document, fileName, IsPublic: false, Credit: null);
}

internal sealed record DemoVehicle(
    VehicleData Data,
    Odometer CurrentOdometer,
    IReadOnlyList<DemoRecord> Records,
    IReadOnlyList<DemoReminder> Reminders,
    DemoShareLink? PublicLink);

internal sealed record DemoRecord(RecordDetails Details, IReadOnlyList<DemoAttachment> Attachments);

/// <summary>
/// Um arquivo do registro: o nome do asset embutido (<see cref="Garage.Application.Abstractions.Ports.IDemoAssets"/>),
/// como aparece na lista de anexos, se vai para o link público (só foto) e, nas fotos, o autor e a licença do crédito.
/// </summary>
internal sealed record DemoAttachment(string Asset, AttachmentKind Kind, string FileName, bool IsPublic, string? Credit);

internal sealed record DemoReminder(string Title, ReminderInterval Interval, DateOnly LastDoneDate, Odometer? LastDoneOdometer);

internal sealed record DemoShareLink(string Label, bool IncludeCosts, bool IncludePlate);
