import { IoBeerOutline } from 'solid-icons/io';
import Head from "./Head";

const About = () => (
  <div>
	<Head title="About Us - SolidJS" description="Learn more about our SolidJS app" />
    <h1>About Page</h1>
    <IoBeerOutline style={{ "font-size": '48px', color: 'orange' }} />
  </div>
);

export default About;

