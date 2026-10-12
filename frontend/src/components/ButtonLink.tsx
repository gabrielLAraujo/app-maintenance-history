import { Link, type LinkProps } from 'react-router'
import { buttonClass, type ButtonVariant } from './buttonStyles'

type Props = LinkProps & { variant?: ButtonVariant }

/** Link de navegação com a aparência de um botão (para quem só leva a outra tela, não envia nada). */
export function ButtonLink({ variant = 'primary', className = '', ...rest }: Props) {
  return <Link className={buttonClass(variant, className)} {...rest} />
}
