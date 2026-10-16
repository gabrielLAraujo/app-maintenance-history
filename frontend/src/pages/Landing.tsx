import { useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { problemMessage, type Problem } from '../api/problem'
import { useAuth } from '../auth/AuthProvider'
import { Alert } from '../components/Alert'
import { Badge } from '../components/Badge'
import { Button } from '../components/Button'
import { buttonClass } from '../components/buttonStyles'
import { Logo } from '../components/Logo'
import { PlateChip } from '../components/PlateChip'

const REPOSITORY = 'https://github.com/gabrielLAraujo/app-maintenance-history'
const ARCHITECTURE = `${REPOSITORY}/blob/main/docs/ARQUITETURA.md`
const PHOTO_CREDITS = `${REPOSITORY}/blob/main/backend/src/Garage.Infrastructure/Demo/Assets/CREDITS.md`

/** Testes automatizados do repositório (back e front) na data da entrega desta tela. */
const TEST_COUNT = '872'

function demoProblemMessage(problem: Problem): string {
  if (problem.status === 429) return 'Muitas demonstrações criadas a partir deste endereço. Tente de novo em alguns minutos.'
  if (problem.status === 503) return 'A demonstração está cheia agora. Tente de novo mais tarde.'
  return problemMessage(problem)
}

function ArrowIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      className="size-[18px]"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M5 12h14" />
      <path d="M13 6l6 6-6 6" />
    </svg>
  )
}

type DemoButtonProps = {
  size?: 'md' | 'lg'
  className?: string
}

/**
 * "Ver demo": abre uma conta demo já logada (com os dados de exemplo) e o app segue para o painel. Os erros (limite
 * por endereço, demo cheia) aparecem logo abaixo do botão.
 */
export function DemoButton({ size = 'md', className = '' }: DemoButtonProps) {
  const { startDemo } = useAuth()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleClick() {
    setPending(true)
    setError(null)
    const problem = await startDemo()
    // Deu certo: a sessão abriu e esta tela sai de cena (landing ou login); nada mais a fazer aqui.
    if (!problem) return

    setError(demoProblemMessage(problem))
    setPending(false)
  }

  return (
    <div className={`flex flex-col gap-2 ${className}`}>
      <Button
        loading={pending}
        onClick={() => void handleClick()}
        className={size === 'lg' ? 'min-h-[52px] rounded-xl px-6 text-[17px]' : ''}
      >
        Ver demo
        {!pending && <ArrowIcon />}
      </Button>
      {error && <Alert variant="error">{error}</Alert>}
    </div>
  )
}

function SignInLink({ size = 'md' }: { size?: 'md' | 'lg' }) {
  return (
    <Link to="/login" className={buttonClass('secondary', size === 'lg' ? 'min-h-[52px] rounded-xl px-6 text-[17px]' : '')}>
      Entrar
    </Link>
  )
}

function Eyebrow({ children, inverse = false }: { children: ReactNode; inverse?: boolean }) {
  return <p className={`eyebrow ${inverse ? 'text-amber' : 'text-amber-ink'}`}>{children}</p>
}

function Feature({ icon, title, children }: { icon: ReactNode; title: string; children: ReactNode }) {
  return (
    <article className="flex flex-col gap-3 rounded-card border border-line p-6">
      <span className="inline-flex size-11 items-center justify-center rounded-xl bg-amber-soft text-amber-ink">{icon}</span>
      <h3 className="text-xl font-bold">{title}</h3>
      <p className="text-ink-2">{children}</p>
    </article>
  )
}

function FeatureIcon({ children }: { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      className="size-[22px]"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  )
}

function Check({ children }: { children: ReactNode }) {
  return (
    <li className="flex items-start gap-2.5">
      <svg
        viewBox="0 0 24 24"
        className="mt-0.5 size-5 flex-none text-ok"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M20 6L9 17l-5-5" />
      </svg>
      <span>{children}</span>
    </li>
  )
}

function Stat({ value, children }: { value: string; children: ReactNode }) {
  return (
    <div className="min-w-0 flex-[1_1_220px] rounded-card border border-[#2E333D] p-5">
      <p className="font-mono text-[34px] leading-tight font-semibold text-white">{value}</p>
      <p className="mt-1 text-[#A3AAB8]">{children}</p>
    </div>
  )
}

function SampleRecord({ photo, alt, meta, title, children }: { photo: string; alt: string; meta: string; title: string; children: string }) {
  return (
    <div className="flex gap-3.5 border-t border-track pt-4">
      <img src={photo} alt={alt} width={208} height={139} loading="lazy" className="h-[78px] w-[104px] flex-none rounded-control object-cover" />
      <div className="min-w-0">
        <p className="font-mono text-xs text-muted">{meta}</p>
        <p className="mt-0.5 font-bold">{title}</p>
        <p className="mt-0.5 text-sm text-ink-2">{children}</p>
      </div>
    </div>
  )
}

/** Início para quem não tem sessão: o que é a Garagem, a demo em um clique e como foi construída. */
export function Landing() {
  const { demoEnabled } = useAuth()

  return (
    <div className="bg-ground text-ink">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex max-w-[1200px] flex-wrap items-center gap-x-7 gap-y-3 px-4 py-3.5 sm:px-6">
          <Link to="/" aria-label="Garagem, início" className="rounded-control focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ink">
            <Logo />
          </Link>
          <nav aria-label="Principal" className="hidden gap-6 text-[15px] font-medium md:flex">
            <a href="#recursos" className="hover:text-amber-ink">
              Recursos
            </a>
            <a href="#link-publico" className="hover:text-amber-ink">
              Link para venda
            </a>
            <a href="#por-dentro" className="hover:text-amber-ink">
              Por dentro
            </a>
          </nav>
          <div className="ml-auto flex flex-wrap items-start gap-2.5">
            <SignInLink />
            {demoEnabled && (
              <div className="hidden sm:block">
                <DemoButton />
              </div>
            )}
          </div>
        </div>
      </header>

      <main>
        <section className="mx-auto flex max-w-[1200px] flex-wrap items-center gap-10 px-4 pt-12 pb-14 sm:px-6 lg:gap-14 lg:pt-[72px] lg:pb-16">
          <div className="flex min-w-0 flex-[1_1_440px] flex-col gap-6">
            <Eyebrow>Histórico de manutenção e modificações</Eyebrow>
            <h1 className="font-display text-[40px] leading-[1.04] tracking-[-0.025em] sm:text-5xl lg:text-[56px]">
              Tudo o que já foi feito no seu carro, num só lugar.
            </h1>
            <p className="max-w-[540px] text-lg leading-relaxed text-ink-2 sm:text-[19px]">
              Registre manutenções e modificações com peças, custos, fotos e notas fiscais. Receba lembretes por km ou
              por tempo e mostre o histórico a quem vai comprar, com um link só de leitura.
            </p>
            <div className="flex flex-wrap items-start gap-3">
              {demoEnabled && <DemoButton size="lg" />}
              <SignInLink size="lg" />
            </div>
            {demoEnabled && (
              <p className="text-sm text-muted">Sem cadastro: a demo abre com um Civic 1998 e um Gol 2019 já preenchidos.</p>
            )}
          </div>

          <div className="relative min-w-0 flex-[1_1_520px]">
            <figure>
              <img
                src="/landing/civic.jpg"
                srcSet="/landing/civic-800.jpg 800w, /landing/civic.jpg 1600w"
                sizes="(min-width: 1200px) 560px, (min-width: 1024px) 50vw, 100vw"
                alt="Honda Civic sedã 1998 visto pela frente, do lado esquerdo"
                width={1600}
                height={749}
                fetchPriority="high"
                className="block h-[280px] w-full rounded-cover object-cover sm:h-[440px]"
              />
              <figcaption className="mt-2 text-xs text-muted">Foto: Elise240SX, Wikimedia Commons (CC BY-SA 4.0)</figcaption>
            </figure>
            <div className="mt-4 flex flex-col gap-3 rounded-card border border-line bg-surface p-4 shadow-[0_12px_32px_rgba(20,22,27,0.18)] sm:absolute sm:bottom-[52px] sm:left-5 sm:mt-0 sm:w-[300px]">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-[17px] font-bold">Civic EK</p>
                  <p className="font-mono text-[15px] font-semibold">152.300 km</p>
                </div>
                <PlateChip plate="ABC1D23" />
              </div>
              <div className="flex items-center gap-2.5 rounded-control bg-danger-soft px-3 py-2.5">
                <span className="min-w-0 flex-auto">
                  <span className="block text-sm font-semibold">Fluido de freio</span>
                  <span className="block text-[13px] text-ink-2">venceu há 34 dias</span>
                </span>
                <span className="text-xs font-bold text-danger">Vencido</span>
              </div>
              <div className="flex items-center gap-2.5 rounded-control bg-amber-soft px-3 py-2.5">
                <span className="min-w-0 flex-auto">
                  <span className="block text-sm font-semibold">Troca de óleo e filtro</span>
                  <span className="block text-[13px] text-ink-2">faltam 500 km ou 49 dias</span>
                </span>
                <span className="text-xs font-bold text-amber-ink">Próximo</span>
              </div>
            </div>
          </div>
        </section>

        <section id="recursos" aria-labelledby="recursos-titulo" className="border-y border-line bg-surface">
          <div className="mx-auto flex max-w-[1200px] flex-col gap-10 px-4 py-14 sm:px-6 lg:py-[72px]">
            <div className="flex max-w-[680px] flex-col gap-3">
              <Eyebrow>Recursos</Eyebrow>
              <h2 id="recursos-titulo" className="font-display text-3xl leading-tight sm:text-[38px]">
                Feito para quem cuida do carro de verdade.
              </h2>
            </div>
            <div className="grid gap-5 md:grid-cols-3">
              <Feature
                title="Histórico completo"
                icon={
                  <FeatureIcon>
                    <path d="M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2.4-.6-.6-2.4z" />
                  </FeatureIcon>
                }
              >
                Cada serviço com peças, quantidades, mão de obra, fotos e PDFs. A km nunca volta: cada registro precisa
                caber entre o anterior e o seguinte.
              </Feature>
              <Feature
                title="Lembretes por km ou tempo"
                icon={
                  <FeatureIcon>
                    <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
                    <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
                  </FeatureIcon>
                }
              >
                Óleo a cada 5.000 km ou 6 meses, o que vier primeiro. O painel mostra o que já venceu e o que está perto,
                de todos os carros.
              </Feature>
              <Feature
                title="Quanto custa manter"
                icon={
                  <FeatureIcon>
                    <path d="M3 3v18h18" />
                    <path d="M7 15v3" />
                    <path d="M12 9v9" />
                    <path d="M17 12v6" />
                  </FeatureIcon>
                }
              >
                Gasto total, por categoria e por mês, e o custo por km rodado. Manutenção e modificação separadas, para
                saber o que é cuidado e o que é projeto.
              </Feature>
            </div>
          </div>
        </section>

        <section
          id="link-publico"
          aria-labelledby="link-publico-titulo"
          className="mx-auto flex max-w-[1200px] flex-wrap items-center gap-10 px-4 py-16 sm:px-6 lg:gap-14 lg:py-20"
        >
          <div className="flex min-w-0 flex-[1_1_440px] flex-col gap-5">
            <Eyebrow>Link para venda</Eyebrow>
            <h2 id="link-publico-titulo" className="font-display text-3xl leading-tight sm:text-[38px]">
              Na hora de vender, o histórico fala por você.
            </h2>
            <p className="text-lg text-ink-2">
              Gere um link só de leitura com a km, os serviços e as fotos que você marcar. Custos e placa aparecem só se
              você quiser. Chassi, documentos, notas fiscais e lembretes nunca aparecem.
            </p>
            <ul className="flex flex-col gap-2.5">
              <Check>Quilometragem validada em cada registro.</Check>
              <Check>Fotos sem localização: EXIF e GPS são removidos no upload.</Check>
              <Check>Você revoga o link quando quiser.</Check>
            </ul>
          </div>

          <div className="flex min-w-0 flex-[1_1_480px] flex-col gap-4 rounded-cover border border-line bg-surface p-6 shadow-[0_8px_24px_rgba(20,22,27,0.06)]">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-lg font-bold">Honda Civic 1998 · EK</p>
                <p className="text-sm text-muted">6 serviços · histórico desde nov/2024</p>
              </div>
              <Badge tone="maintenance" className="gap-1.5 rounded-full px-2.5 py-1">
                <svg
                  viewBox="0 0 24 24"
                  className="size-3.5"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
                >
                  <rect x="4" y="11" width="16" height="10" rx="2" />
                  <path d="M8 11V7a4 4 0 0 1 8 0v4" />
                </svg>
                Só leitura
              </Badge>
            </div>
            <SampleRecord photo="/landing/coilover.jpg" alt="Kit coilover rosqueável" meta="16/12/2025 · 142.200 km" title="Coilover">
              Kit rosqueável BC Racing. Amortecedores originais guardados.
            </SampleRecord>
            <SampleRecord
              photo="/landing/correia.jpg"
              alt="Correia dentada de motor"
              meta="11/11/2024 · 128.900 km"
              title="Troca da correia dentada"
            >
              Kit completo com tensor; bomba d'água trocada junto.
            </SampleRecord>
          </div>
        </section>

        <section id="por-dentro" aria-labelledby="por-dentro-titulo" className="bg-ink text-ground">
          <div className="mx-auto flex max-w-[1200px] flex-col gap-10 px-4 py-16 sm:px-6 lg:py-20">
            <div className="flex max-w-[760px] flex-col gap-3.5">
              <Eyebrow inverse>Por dentro</Eyebrow>
              <h2 id="por-dentro-titulo" className="font-display text-3xl leading-tight text-white sm:text-[38px]">
                Construído como software de produção.
              </h2>
              <p className="text-lg text-[#C9CED8]">
                Garagem é um projeto pessoal de Gabriel Leite Araujo. Nasceu para documentar um Civic EK em restauração e
                virou um estudo de arquitetura em .NET.
              </p>
            </div>
            <div className="flex flex-wrap gap-4">
              <Stat value={TEST_COUNT}>testes automatizados, back e front</Stat>
              <Stat value=".NET 10">Minimal APIs, EF Core e PostgreSQL; React 19 no front</Stat>
              <Stat value="DDD">Clean Architecture e CQRS, fronteiras garantidas por testes</Stat>
              <Stat value="Coolify">numa VPS, com HTTPS via Traefik e DNS na Cloudflare</Stat>
            </div>
            <div className="flex flex-wrap items-center justify-between gap-6 border-t border-[#2E333D] pt-7">
              <p className="max-w-[680px] text-[#C9CED8]">
                Feito com agentes de IA num fluxo orientado por especificação. O produto, as regras de negócio, a
                arquitetura e a revisão de cada etapa são meus; o README registra o que foi de quem.
              </p>
              <div className="flex flex-wrap gap-2.5">
                <a href={REPOSITORY} className={buttonClass('primary')}>
                  <svg
                    viewBox="0 0 24 24"
                    className="size-[18px]"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden="true"
                  >
                    <path d="M8 6l-6 6 6 6" />
                    <path d="M16 6l6 6-6 6" />
                  </svg>
                  Ver o código
                </a>
                <a href={ARCHITECTURE} className={buttonClass('inverse')}>
                  Ler a arquitetura
                </a>
              </div>
            </div>
          </div>
        </section>

        <section aria-labelledby="chamada-titulo" className="mx-auto max-w-[1200px] px-4 py-14 sm:px-6 lg:py-[72px]">
          <div className="flex flex-wrap items-center justify-between gap-6 rounded-3xl border border-line bg-surface p-6 sm:p-10">
            <div className="flex flex-col gap-1.5">
              <h2 id="chamada-titulo" className="font-display text-[26px] leading-tight sm:text-[30px]">
                {demoEnabled ? 'Veja funcionando em dez segundos.' : 'Seu histórico, do jeito certo.'}
              </h2>
              <p className="text-ink-2">
                {demoEnabled
                  ? 'A demo abre já logada, com dois carros de exemplo. Cada visita tem a própria cópia, apagada em 24 horas.'
                  : 'Entre para registrar serviços, lembretes e gastos dos seus carros.'}
              </p>
            </div>
            {demoEnabled ? <DemoButton size="lg" /> : <SignInLink size="lg" />}
          </div>
        </section>
      </main>

      <footer className="border-t border-line bg-surface">
        <div className="mx-auto flex max-w-[1200px] flex-wrap items-center justify-between gap-x-8 gap-y-4 px-4 py-7 text-sm text-muted sm:px-6">
          <p>
            <span className="font-extrabold text-ink [font-stretch:118%]">Garagem</span> · projeto de Gabriel Leite Araujo
          </p>
          <nav aria-label="Rodapé" className="flex flex-wrap gap-5">
            <a href="https://github.com/gabrielLAraujo" className="underline-offset-2 hover:text-ink hover:underline">
              GitHub
            </a>
            <a href="https://www.linkedin.com/in/gabrielleitearaujo/" className="underline-offset-2 hover:text-ink hover:underline">
              LinkedIn
            </a>
            <a href={PHOTO_CREDITS} className="underline-offset-2 hover:text-ink hover:underline">
              Créditos das fotos
            </a>
          </nav>
        </div>
      </footer>
    </div>
  )
}
