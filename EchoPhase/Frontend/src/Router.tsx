import { Component, lazy } from "solid-js";
import { Router } from '@solidjs/router';

interface RouterComponentProps {
	base?: string,
	root: Component
}

const routes = [
	{
		path: "/",
		component: lazy(() => import("./components/Home"))
	},
	{
		path: "/about",
		component: lazy(() => import("./components/About"))
	},
	{
		path: "/login",
		component: lazy(() => import("./components/Login"))
	},
	{
		path: "/register",
		component: lazy(() => import("./components/Register"))
	},
	{
		path: "/drag",
		component: lazy(() => import("./components/DragExample"))
	},
	{
		path: "/spin",
		component: lazy(() => import("./components/SpinTest"))
	},
	{
		path: "/tokens",
		component: lazy(() => import("./components/TokenList"))
	},
	{
		path: "*404",
		component: lazy(() => import("./components/NotFound"))
	}
];

const RouterComponent = (props: RouterComponentProps) => (
  <Router base={props.base} root={props.root}>
	{routes}
  </Router>
);

export default RouterComponent;
