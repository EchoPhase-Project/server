import { A } from '@solidjs/router';
import { FiHome } from 'solid-icons/fi';
import { IoBeerOutline, IoLink, IoLogIn } from 'solid-icons/io';
import type { ParentProps } from "solid-js";

const Layout = (props: ParentProps) => (
  <div>
	<nav>
	  <A href="/" style={{ margin: '10px' }}>
		<FiHome style={{ 'font-size': '24px', 'margin-right': '5px' }} />
		Home
	  </A>
	  <A href="/about" style={{ margin: '10px' }}>
		<IoBeerOutline style={{ 'font-size': '24px', 'margin-right': '5px' }} />
		About
	  </A>
	  <A href="/login" style={{ margin: '10px' }}>
		<IoLogIn style={{ 'font-size': '24px', 'margin-right': '5px' }} />
		Login
	  </A>
	  <A href="/register" style={{ margin: '10px' }}>
		<IoLink style={{ 'font-size': '24px', 'margin-right': '5px' }} />
		Register
	  </A>
	</nav>
	{props.children}
  </div>
);

export default Layout;
